import {
    Button,
    createTableColumn,
    DataGrid,
    DataGridBody,
    DataGridCell,
    DataGridHeader,
    DataGridHeaderCell,
    DataGridProps,
    DataGridRow,
    TableCellLayout,
    TableColumnSizingOptions,
} from '@fluentui/react-components';
import { Add20Regular, Delete20Regular } from '@fluentui/react-icons';
import { FieldArray, FieldArrayRenderProps, FieldHookConfig, FormikTouched, getIn, useFormikContext } from 'formik';
import { useCallback, useEffect, useMemo, useRef } from 'react';
import { useIntl } from 'react-intl';
import { SreAgentResources } from '../../../Strings/SREAgentResources';

// Debouncing constants for preventing rapid successive row additions
const DEBOUNCE_MS = 50; // 50ms debounce

type EditableGridFormikProps<T> = Omit<DataGridProps, 'items'> &
    Pick<FieldHookConfig<string | undefined>, 'name'> & {
        emptyRow: T;
        isRowTouched?: (index: number) => boolean;
        deleteButtonDisabled?: boolean;
        maxRows?: number;
        readOnly?: boolean;
        addButton?: boolean;
        canDeleteLastItem?: boolean;
        disableAddRowOnTouch?: boolean;
        logEntryAdded?: () => void;
        logEntryDeleted?: () => void;
        placeholders?: number;
    };

type EditableGridProps<T> = EditableGridFormikProps<T> & { fieldArrayRenderProps: FieldArrayRenderProps };

const EditableGridFormik = <T,>(props: EditableGridFormikProps<T>) => {
    return (
        <FieldArray name={props.name}>
            {fieldArrayRenderProps => <EditableGrid<T> {...props} fieldArrayRenderProps={fieldArrayRenderProps} />}
        </FieldArray>
    );
};

const EditableGrid = <T,>(props: EditableGridProps<T>) => {
    const {
        name,
        columns,
        columnSizingOptions,
        emptyRow,
        isRowTouched,
        deleteButtonDisabled,
        maxRows,
        readOnly,
        addButton,
        canDeleteLastItem,
        disableAddRowOnTouch,
        logEntryAdded,
        logEntryDeleted,
        fieldArrayRenderProps: { push, remove },
        placeholders,
    } = props;

    const {
        validateOnBlur,
        validateOnChange,
        setFieldTouched,
        touched: formTouched,
        values: formValues,
        errors: formErrors,
    } = useFormikContext();

    const intl = useIntl();

    useEffect(() => {
        if (placeholders && placeholders > 1) {
            for (let i = 0; i < placeholders - 1; i++) {
                push({ ...emptyRow });
            }
        }
    }, [emptyRow, placeholders, push]);

    const touched = useMemo(() => (getIn(formTouched, name) ?? []) as FormikTouched<T>[], [formTouched, name]);
    const values = useMemo(() => (getIn(formValues, name) ?? []) as T[], [formValues, name]);
    const error = getIn(formErrors, name);

    const prevValuesLength = useRef(values.length);

    // Track last add time per index to prevent rapid successive row additions for the same index
    const lastRowAddTimeRef = useRef<Record<number, number>>({});

    const defaultedIsRowTouched = useCallback(
        (index: number) => {
            if (touched) {
                const row = touched[index];
                const isSomeFieldTouched = row && Object.values(row).some(isTouched => !!isTouched);

                const rowValues: T | undefined = values?.[index];
                const isSomeFieldFilledOut = rowValues && Object.values(rowValues).some(value => !!value);

                return isSomeFieldTouched && isSomeFieldFilledOut;
            }
        },
        [touched, values]
    );

    const getColumns = () => {
        const newColumns = [...(columns ?? [])];

        if (!readOnly) {
            newColumns.push(
                createTableColumn<T>({
                    columnId: 'delete',
                    renderHeaderCell: () => intl.formatMessage(SreAgentResources.delete),
                    renderCell: item => {
                        const itemIndex = values.indexOf(item);

                        // Don't show delete button if there's only 1 row and canDeleteLastItem is false
                        if (values.length <= 1 && !canDeleteLastItem) return null;

                        // Don't show delete button for the last row if canDeleteLastItem is false
                        if (!canDeleteLastItem && itemIndex >= values.length - 1) return null;

                        return (
                            <TableCellLayout>
                                <Button
                                    icon={<Delete20Regular />}
                                    onClick={() => {
                                        logEntryDeleted?.();
                                        remove(itemIndex);
                                    }}
                                    disabled={deleteButtonDisabled}
                                />
                            </TableCellLayout>
                        );
                    },
                })
            );
        }

        return newColumns;
    };

    const modifiedColumns = getColumns();

    const canAddRow = useMemo(() => !readOnly && (maxRows === undefined || values.length < maxRows), [readOnly, maxRows, values.length]);

    useEffect(() => {
        // When deleting a row, validateOnBlur is true and validateOnChange false, the error/touched are updated only on the deleted row.
        // For other rows whose validation depends on the deleted row, we need to manually call setFieldTouched to trigger validation.
        if (validateOnBlur && !validateOnChange) {
            const prevLength = prevValuesLength.current;
            prevValuesLength.current = values.length;
            if (prevLength > values.length && values.length > 0 && !!error) {
                setFieldTouched(name, true);
            }
        }
    }, [values.length, validateOnBlur, validateOnChange, name, error, setFieldTouched]);

    useEffect(() => {
        const lastIndex = values.length - 1;

        if (!disableAddRowOnTouch && canAddRow && (isRowTouched ? isRowTouched(lastIndex) : defaultedIsRowTouched(lastIndex))) {
            const now = Date.now();
            const lastAddTimeForIndex = lastRowAddTimeRef.current[lastIndex] || 0;
            const timeSinceLastAdd = now - lastAddTimeForIndex;

            if (timeSinceLastAdd > DEBOUNCE_MS) {
                lastRowAddTimeRef.current[lastIndex] = now;
                push({ ...emptyRow });
            }
        }
    }, [values.length, disableAddRowOnTouch, canAddRow, isRowTouched, defaultedIsRowTouched, emptyRow, push]);

    const modifiedColumnSizingOptions: TableColumnSizingOptions = useMemo(() => {
        return { ...columnSizingOptions, delete: { idealWidth: 32, minWidth: 32, defaultWidth: 32 } };
    }, [columnSizingOptions]);

    return (
        <>
            {addButton && (
                <Button
                    disabled={!canAddRow}
                    icon={<Add20Regular />}
                    onClick={() => {
                        logEntryAdded?.();
                        push({ ...emptyRow });
                    }}
                >
                    {intl.formatMessage(SreAgentResources.add)}
                </Button>
            )}
            <DataGrid
                {...props}
                items={values}
                columns={modifiedColumns}
                resizableColumns
                columnSizingOptions={modifiedColumnSizingOptions}
            >
                <DataGridHeader>
                    <DataGridRow>{({ renderHeaderCell }) => <DataGridHeaderCell>{renderHeaderCell()}</DataGridHeaderCell>}</DataGridRow>
                </DataGridHeader>
                <DataGridBody<T>>
                    {({ item, rowId }) => (
                        <DataGridRow<T> key={rowId}>{({ renderCell }) => <DataGridCell>{renderCell(item)}</DataGridCell>}</DataGridRow>
                    )}
                </DataGridBody>
            </DataGrid>
        </>
    );
};

export default EditableGridFormik;
