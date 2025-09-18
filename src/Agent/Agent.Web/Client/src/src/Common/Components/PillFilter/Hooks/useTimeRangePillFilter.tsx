import { DatePicker, initializeIcons, TimePicker } from '@fluentui/react';
import { Field, makeStyles, Radio, RadioGroup } from '@fluentui/react-components';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { SreAgentResources } from '../../../../Strings/SREAgentResources';
import {
    changeToLocalTimezone,
    changeToUtcTimezone,
    extractDateFromDateTime,
    formatDateToYYYYMMDD,
    getCombineDateAndTime,
} from '../../../Helpers/Date';
import { TimeRangeKeyLabelPair, TimeRangeValue, TimespanKeys, UseTimeRangePillFilterProps } from '../Contracts';

const parseSelectedValue = (value: TimeRangeValue | undefined, defaultKey: string): TimeRangeValue | undefined => {
    if (!value) return undefined;
    const { key, start, end } = value;
    return {
        key: key || defaultKey,
        start: changeToLocalTimezone(start),
        end: changeToLocalTimezone(end),
    };
};

const useTimeRangePillFilterStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        gap: '10px',
        height: '100%',
        overflowY: 'auto',
        paddingRight: '10px',
        position: 'relative',
    },
    radioGroup: {
        position: 'relative',
        fontSize: '13px',
        zIndex: 1,
        margin: '2px 8px',
    },
    dateTimeField: {
        marginLeft: '16px',
    },
    dateTimeWrapper: {
        display: 'flex',
        flexDirection: 'row',
        gap: '8px',
    },
    datePicker: {
        flexBasis: '120px',
        minWidth: '120px',
    },
    timePicker: {
        flexBasis: '80px',
        minWidth: '80px',
        '& .ms-Button-icon': {
            fontSize: '16px',
            height: '22px',
        },
    },
});

export const useTimeRangePillFilter = (props: UseTimeRangePillFilterProps | undefined) => {
    const options = useMemo(() => props?.options || [], [props?.options]);
    const onApply = useCallback((value: TimeRangeValue) => props?.onApply(value), [props?.onApply]);
    const selectedValue = useMemo(() => props?.selectedValue, [props?.selectedValue]);
    const customTimeRangeProps = useMemo(() => props?.customTimeRangeProps, [props?.customTimeRangeProps]);
    const disabled = useMemo(() => props?.disabled || false, [props?.disabled]);

    const intl = useIntl();
    const styles = useTimeRangePillFilterStyles();
    const [iconsInitialized, setIconsInitialized] = useState(false);
    const [currentSelectedValue, setCurrentSelectedValue] = useState<TimeRangeValue | undefined>(
        parseSelectedValue(selectedValue, options[0]?.key)
    );

    const [pendingSelectedKey, setPendingSelectedKey] = useState<string | undefined>(selectedValue?.key || options[0]?.key);
    const [pendingStartDate, setPendingStartDate] = useState<Date | undefined>();
    const [pendingStartTime, setPendingStartTime] = useState<Date | undefined>();
    const [pendingEndDate, setPendingEndDate] = useState<Date | undefined>();
    const [pendingEndTime, setPendingEndTime] = useState<Date | undefined>();

    const { addCustomOption, customOptionLabel } = useMemo(() => customTimeRangeProps || {}, [customTimeRangeProps]);
    const customLabel = useMemo(() => customOptionLabel || intl.formatMessage(SreAgentResources.custom), [customOptionLabel, intl]);

    const optionsList: TimeRangeKeyLabelPair[] = useMemo(() => {
        if (addCustomOption) {
            return [...options, { key: TimespanKeys.Custom, label: customLabel }];
        }

        return options;
    }, [options, addCustomOption, customLabel]);

    const getOptionText = useCallback(
        (value: string): string => {
            const option = optionsList.find(option => option.key === value);
            return option ? option.label : value;
        },
        [optionsList]
    );

    const pillDisplayValue = useMemo((): string => {
        return currentSelectedValue?.key ? getOptionText(currentSelectedValue.key) : '';
    }, [getOptionText, currentSelectedValue]);

    const onApplyClick = useCallback(() => {
        setCurrentSelectedValue({
            key: pendingSelectedKey || '',
            start: pendingStartTime || undefined,
            end: pendingEndTime || undefined,
        });
        onApply({
            key: pendingSelectedKey || '',
            start: pendingSelectedKey === TimespanKeys.Custom ? changeToUtcTimezone(pendingStartTime) : undefined,
            end: pendingSelectedKey === TimespanKeys.Custom ? changeToUtcTimezone(pendingEndTime) : undefined,
        });
    }, [pendingSelectedKey, pendingStartTime, pendingEndTime, onApply]);

    const isComplete = useMemo(() => {
        if (!pendingSelectedKey) {
            return false;
        }

        if (!addCustomOption || pendingSelectedKey !== TimespanKeys.Custom) {
            return true;
        }

        return !!pendingStartTime && !!pendingEndTime;
    }, [addCustomOption, pendingSelectedKey, pendingStartTime, pendingEndTime]);

    const initializeLocalState = useCallback(() => {
        const end = selectedValue?.end ? changeToLocalTimezone(selectedValue.end) : changeToLocalTimezone(new Date());

        const start = selectedValue?.start ? changeToLocalTimezone(selectedValue.start) : new Date(end!.getTime() - 60 * 60 * 1000); // Default to 1 hour before end time

        setCurrentSelectedValue({
            key: selectedValue?.key || '',
            start,
            end,
        });
        setPendingSelectedKey(selectedValue?.key || '');
        setPendingStartDate(extractDateFromDateTime(start));
        setPendingStartTime(start);
        setPendingEndDate(extractDateFromDateTime(end));
        setPendingEndTime(end);
    }, [selectedValue]);

    useEffect(() => {
        initializeLocalState();
    }, [initializeLocalState]);

    useEffect(() => {
        initializeIcons();
        setIconsInitialized(true);
    }, []);

    const onRenderPopoverContent = useCallback(() => {
        return (
            <div className={styles.root}>
                <>
                    <RadioGroup
                        value={pendingSelectedKey || ''}
                        className={styles.radioGroup}
                        disabled={disabled || !iconsInitialized}
                        onChange={(_, data) => setPendingSelectedKey(data.value)}
                    >
                        <>
                            {optionsList.map(option => (
                                <Radio key={option.key} value={option.key} label={option.label} />
                            ))}
                        </>
                    </RadioGroup>
                </>
                {addCustomOption && pendingSelectedKey === TimespanKeys.Custom && (
                    <>
                        <Field className={styles.dateTimeField} label={intl.formatMessage(SreAgentResources.start)} required>
                            <div className={styles.dateTimeWrapper}>
                                <DatePicker
                                    // TODO (andimarc): implement min/max date logic
                                    pickerAriaLabel={intl.formatMessage(SreAgentResources.startDatePickerAriaLabel)}
                                    ariaLabel={intl.formatMessage(SreAgentResources.startDateAriaLabel)}
                                    className={styles.datePicker}
                                    value={pendingStartDate || undefined}
                                    onSelectDate={date => {
                                        if (date) {
                                            setPendingStartDate(date);
                                            setPendingStartTime(getCombineDateAndTime(date, pendingStartTime));
                                        }
                                    }}
                                    formatDate={date => formatDateToYYYYMMDD(date)}
                                    showGoToToday={true}
                                    isMonthPickerVisible={true}
                                />
                                <TimePicker
                                    // TODO (andimarc): implement min/max time logic
                                    ariaLabel={intl.formatMessage(SreAgentResources.startTimeAriaLabel)}
                                    className={styles.timePicker}
                                    dateAnchor={pendingStartDate ?? undefined}
                                    increments={60}
                                    value={pendingStartTime || undefined}
                                    onChange={(_, time) => {
                                        setPendingStartTime(time);
                                    }}
                                    buttonIconProps={{ iconName: 'Clock' }}
                                    disabled={disabled || !pendingStartDate}
                                />
                            </div>
                        </Field>
                        <Field className={styles.dateTimeField} label={intl.formatMessage(SreAgentResources.end)} required>
                            <div className={styles.dateTimeWrapper}>
                                <DatePicker
                                    // TODO (andimarc): implement min/max date logic
                                    pickerAriaLabel={intl.formatMessage(SreAgentResources.endDatePickerAriaLabel)}
                                    ariaLabel={intl.formatMessage(SreAgentResources.endDateAriaLabel)}
                                    className={styles.datePicker}
                                    value={pendingEndDate || undefined}
                                    onSelectDate={date => {
                                        if (date) {
                                            setPendingEndDate(date);
                                            setPendingEndTime(getCombineDateAndTime(date, pendingEndTime));
                                        }
                                    }}
                                    formatDate={date => formatDateToYYYYMMDD(date)}
                                    showGoToToday={true}
                                    isMonthPickerVisible={true}
                                />
                                <TimePicker
                                    // TODO (andimarc): implement min/max time logic
                                    ariaLabel={intl.formatMessage(SreAgentResources.endTimeAriaLabel)}
                                    className={styles.timePicker}
                                    dateAnchor={pendingEndDate ?? undefined}
                                    increments={60}
                                    value={pendingEndTime || undefined}
                                    onChange={(_, time) => {
                                        setPendingEndTime(time);
                                    }}
                                    buttonIconProps={{ iconName: 'Clock' }}
                                    disabled={disabled || !pendingEndDate}
                                />
                            </div>
                        </Field>
                    </>
                )}
            </div>
        );
    }, [
        addCustomOption,
        disabled,
        iconsInitialized,
        intl,
        optionsList,
        pendingEndDate,
        pendingEndTime,
        pendingSelectedKey,
        pendingStartDate,
        pendingStartTime,
        styles,
    ]);
    return {
        pillDisplayValue,
        onApplyClick,
        isComplete,
        initializeLocalState,
        onRenderPopoverContent,
    };
};
