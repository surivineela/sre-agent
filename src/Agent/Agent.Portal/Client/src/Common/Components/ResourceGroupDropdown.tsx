import {
    Button,
    Combobox,
    ComboboxProps,
    Field,
    Input,
    Link,
    makeStyles,
    mergeClasses,
    OnOpenChangeData,
    OpenPopoverEvents,
    OptionOnSelectData,
    Popover,
    PopoverSurface,
    PopoverTrigger,
    SelectionEvents,
    Skeleton,
    SkeletonItem,
    useComboboxFilter,
} from '@fluentui/react-components';
import { ChangeEvent, useCallback, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { PortalResources } from '../../Strings/Resources';
import { TelemetrySource } from '../Constants/Telemetry';
import { ResourceGroup } from '../Contracts/Arm';
import { usePermissions } from '../Hooks/usePermissions';
import { useResourceGroups } from '../Hooks/useResourceGroups';

type NewableResourceGroup = {
    /** If true, this resource group doesn't exist yet */
    new?: boolean;
} & ResourceGroup;

type ResourceGroupDropdownProps = {
    /**
     * The resource group id to be set as a value for the dropdown if it's a member of the values fetched from ARM.
     */
    readonly selectedResourceGroupId: string | undefined;
    readonly subscriptionId: string | undefined;
    readonly telemetrySource: TelemetrySource;
    readonly 'aria-label'?: string;
    readonly 'aria-labelledby'?: string;
    readonly 'aria-required'?: boolean;
    readonly className?: string;
    /** Adds "Create new" callout under the dropdown. */
    readonly createNew?: boolean;
    /**
     * Callback on resource group change.
     * If selectedResourceGroupId is specified and invalid, this callback will be called with an empty string and null.
     *
     * @param resourceGroup - Selected resource group object with all available properties
     */
    readonly onResourceGroupChange: (resourceGroup?: NewableResourceGroup) => void;
    /**
     * ResourceGroups to be displayed.
     * If provided, the internal 'useResourceGroups' hook will be disabled.
     * The 'useResourceGroups' hook can be called separately to retrieve the resourceGroups from ARM.
     */
    readonly resourceGroups?: NewableResourceGroup[];
    disabled?: boolean;
    errorMessage?: string;
};

const useStyles = makeStyles({
    combobox: {
        minWidth: '250px',
    },
    createNewLink: {
        marginTop: '4px',
    },
    popoverContentContainer: {
        maxWidth: '420px',
    },
    popoverFieldContainer: {
        minHeight: '104px',
    },
    popoverButtonGroup: {
        display: 'flex',
        gap: '20px',
        justifyContent: 'flex-end',
        marginTop: '20px',
    },
});

export const ResourceGroupDropdown = (props: ResourceGroupDropdownProps) => {
    const {
        'aria-label': ariaLabel,
        'aria-labelledby': ariaLabelledBy,
        'aria-required': ariaRequired,
        className,
        createNew,
        onResourceGroupChange,
        resourceGroups: providedResourceGroups,
        selectedResourceGroupId,
        subscriptionId,
        telemetrySource,
        disabled,
        errorMessage: explicitErrorMessage,
    } = props;

    const intl = useIntl();
    const styles = useStyles();

    const [query, setQuery] = useState('');
    const [hasUserChanged, setHasUserChanged] = useState(false);
    const [newResourceGroup, setNewResourceGroup] = useState<NewableResourceGroup>();

    const {
        resourceGroups,
        isLoading: isLoadingResourceGroups,
        error: resourceGroupsError,
    } = useResourceGroups({
        disabled: !!providedResourceGroups,
        subscriptionId,
        telemetrySource,
    });
    const rscGrpsToUse = useMemo(() => providedResourceGroups ?? resourceGroups, [providedResourceGroups, resourceGroups]);

    const armResourceGroups = useMemo(() => {
        return (rscGrpsToUse ?? []).sort((rg1, rg2) => rg1.name.localeCompare(rg2.name));
    }, [rscGrpsToUse]);

    const errorMessage = resourceGroupsError
        ? `${intl.formatMessage(PortalResources.requestError)}: ${resourceGroupsError.message}`
        : undefined;

    const allResourceGroups = useMemo<NewableResourceGroup[]>(
        () => [...(newResourceGroup ? [newResourceGroup] : []), ...armResourceGroups],
        [armResourceGroups, newResourceGroup]
    );

    const resourceGroupOptions = useMemo(
        () =>
            allResourceGroups.map(rg => ({
                children: rg.new ? intl.formatMessage(PortalResources.newItemFormat, { item: rg.name }) : rg.name,
                value: rg.id,
            })),
        [allResourceGroups, intl]
    );

    const children = useComboboxFilter(hasUserChanged ? query : '', resourceGroupOptions, {
        optionToText: option => option.children as string,
        noOptionsMessage: intl.formatMessage(PortalResources.noResultsFound),
    });

    const onOptionSelect = useCallback<NonNullable<ComboboxProps['onOptionSelect']>>(
        (_event: SelectionEvents, data: OptionOnSelectData) => {
            const selectedRgId = data.optionValue;
            const resourceGroup = allResourceGroups.find(rg => rg.id === selectedRgId);
            onResourceGroupChange(resourceGroup);
            setQuery(data.optionText ?? '');
        },
        [onResourceGroupChange, allResourceGroups]
    );

    const onPopoverClose = useCallback(
        (validatedNewResourceGroupName?: string) => {
            if (validatedNewResourceGroupName) {
                const newResourceGroupId = `/subscriptions/${subscriptionId}/resourceGroups/${validatedNewResourceGroupName}`;
                const newRg: NewableResourceGroup = {
                    id: newResourceGroupId,
                    location: '',
                    name: validatedNewResourceGroupName,
                    new: true,
                };
                setNewResourceGroup(newRg);
                onResourceGroupChange(newRg);
                setQuery(intl.formatMessage(PortalResources.newItemFormat, { item: validatedNewResourceGroupName }));
            }
        },
        [intl, onResourceGroupChange, subscriptionId]
    );

    // Auto-select first resource group on load if none is selected
    useEffect(() => {
        if (!isLoadingResourceGroups && allResourceGroups.length > 0 && !selectedResourceGroupId) {
            const firstRg = allResourceGroups[0];
            onResourceGroupChange(firstRg);
            const text = firstRg.new ? intl.formatMessage(PortalResources.newItemFormat, { item: firstRg.name }) : firstRg.name;
            setQuery(text);
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [isLoadingResourceGroups, allResourceGroups.length, selectedResourceGroupId]);

    // Discard the new resource group if subscriptionId changes
    useEffect(() => {
        setNewResourceGroup(undefined);
    }, [subscriptionId]);

    return (
        <Field
            label={intl.formatMessage(PortalResources.resourceGroup)}
            validationMessage={explicitErrorMessage || errorMessage}
            validationState={explicitErrorMessage || errorMessage ? 'error' : undefined}
        >
            {isLoadingResourceGroups ? (
                <Skeleton aria-label={intl.formatMessage(PortalResources.loading)}>
                    <SkeletonItem size={32} />
                </Skeleton>
            ) : (
                <>
                    <Combobox
                        aria-label={ariaLabel || intl.formatMessage(PortalResources.resourceGroup)}
                        aria-labelledby={ariaLabelledBy}
                        aria-required={ariaRequired}
                        className={mergeClasses(styles.combobox, className)}
                        onOptionSelect={onOptionSelect}
                        placeholder={intl.formatMessage(PortalResources.selectAnExistingResourceGroup)}
                        value={query}
                        onChange={ev => {
                            setQuery(ev.target.value);
                            setHasUserChanged(true);
                        }}
                        disabled={disabled}
                    >
                        {children}
                    </Combobox>
                    {createNew && subscriptionId && (
                        <ResourceGroupDropdownPopover
                            className={styles.createNewLink}
                            existingResourceGroupNames={armResourceGroups.map(rg => rg.name)}
                            onClose={onPopoverClose}
                            subscriptionId={subscriptionId}
                            disabled={disabled}
                        />
                    )}
                </>
            )}
        </Field>
    );
};

ResourceGroupDropdown.displayName = 'ResourceGroupDropdown';

type ResourceGroupDropdownPopoverProps = {
    existingResourceGroupNames: string[];
    onClose: (validatedResourceGroupName?: string) => void;
    subscriptionId: string;
    className?: string;
    disabled?: boolean;
};

const ResourceGroupDropdownPopover = (props: ResourceGroupDropdownPopoverProps) => {
    const { className, existingResourceGroupNames, onClose, subscriptionId, disabled } = props;

    const intl = useIntl();

    const [open, setOpen] = useState(false);
    const [resourceGroupName, setResourceGroupName] = useState('');
    const styles = useStyles();

    const { hasPermissions, isLoadingPermissions } = usePermissions({
        entityId: `/subscriptions/${subscriptionId}`,
        actions: ['Microsoft.Resources/subscriptions/resourcegroups/write'],
        telemetrySource: TelemetrySource.SreAgentCreate,
    });

    const resourceGroupAlreadyExists = useMemo(() => {
        return existingResourceGroupNames.includes(resourceGroupName);
    }, [existingResourceGroupNames, resourceGroupName]);

    const onInputChange = useCallback((event: ChangeEvent<HTMLInputElement>) => {
        setResourceGroupName(event.target.value);
    }, []);

    const onOpenChange = useCallback((_e: OpenPopoverEvents, data: OnOpenChangeData) => {
        setOpen(data.open);
    }, []);

    const onCancelClicked = useCallback(() => {
        setOpen(false);
        setResourceGroupName('');
        onClose();
    }, [onClose]);

    const onOkClicked = useCallback(() => {
        setOpen(false);
        onClose(resourceGroupName);
        setResourceGroupName('');
    }, [onClose, resourceGroupName]);

    return (
        <Popover onOpenChange={onOpenChange} open={open} positioning="below-start" trapFocus>
            <PopoverTrigger disableButtonEnhancement>
                <Link className={className} disabled={disabled}>
                    {intl.formatMessage(PortalResources.createNew)}
                </Link>
            </PopoverTrigger>
            <PopoverSurface>
                <div className={styles.popoverContentContainer}>
                    {!isLoadingPermissions && !hasPermissions && (
                        <div style={{ marginTop: '12px', marginBottom: '12px' }}>
                            {intl.formatMessage(PortalResources.noResourceGroupCreatePermission)}
                        </div>
                    )}
                    <div className={styles.popoverFieldContainer}>
                        <Field
                            label={intl.formatMessage(PortalResources.name)}
                            validationMessage={
                                resourceGroupAlreadyExists
                                    ? intl.formatMessage(PortalResources.resourceGroupAlreadyExistsInSubscription)
                                    : undefined
                            }
                            validationState={resourceGroupAlreadyExists ? 'error' : undefined}
                        >
                            <Input disabled={!hasPermissions || isLoadingPermissions} onChange={onInputChange} value={resourceGroupName} />
                        </Field>
                    </div>
                    <div className={styles.popoverButtonGroup}>
                        <Button appearance="secondary" onClick={onCancelClicked}>
                            {intl.formatMessage(PortalResources.cancel)}
                        </Button>
                        <Button
                            appearance="primary"
                            disabled={!resourceGroupName || resourceGroupAlreadyExists || !hasPermissions || isLoadingPermissions}
                            onClick={onOkClicked}
                        >
                            {intl.formatMessage(PortalResources.create)}
                        </Button>
                    </div>
                </div>
            </PopoverSurface>
        </Popover>
    );
};
