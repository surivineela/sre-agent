import { useCallback, useMemo, useState } from 'react';
import { IntlShape } from 'react-intl';
import {
    TriggerDirtyField,
    TriggerScheduleState,
    TriggerState,
    TriggerStateController,
    TriggerUserPatch,
    TriggerValidationState,
} from '../types';
import { DEFAULT_SCHEDULE_PRESET, SCHEDULE_PRESETS } from '../utils/schedule';
import { buildTriggerDefaults, getIncidentDefaults, getIncidentDefaultsMeta, refreshScheduledDefaults } from '../utils/triggerDefaults';

const defaultDirtyState: Record<TriggerDirtyField, boolean> = {
    name: false,
    description: false,
    instructions: false,
    schedule: false,
};

const cloneSchedule = (schedule: TriggerScheduleState): TriggerScheduleState => ({ ...schedule });

export const useTriggerState = (
    intl: IntlShape,
    initial?: Partial<TriggerState>,
    incidentPlatformType?: string
): TriggerStateController => {
    const incidentDefaults = useMemo(() => getIncidentDefaults(intl, initial?.agentDisplayName ?? initial?.agentName), [intl, initial]);
    const scheduledDefaults = useMemo(
        () => refreshScheduledDefaults(intl, initial?.agentDisplayName ?? initial?.agentName),
        [intl, initial]
    );
    const scheduleDefaults = useMemo<TriggerScheduleState>(
        () => ({
            preset: DEFAULT_SCHEDULE_PRESET,
            cronExpression: SCHEDULE_PRESETS[DEFAULT_SCHEDULE_PRESET].cron,
            naturalText: '',
            timezone: Intl.DateTimeFormat().resolvedOptions().timeZone ?? 'UTC',
            startTime: undefined,
        }),
        []
    );

    const [trigger, setTriggerState] = useState<TriggerState>(() => {
        const meta = getIncidentDefaultsMeta(incidentPlatformType);
        return {
            mode: initial?.mode ?? 'incident',
            strategy: initial?.strategy ?? 'quick',
            agentName: initial?.agentName,
            agentDisplayName: initial?.agentDisplayName,
            name: initial?.name ?? incidentDefaults.name,
            description: initial?.description ?? scheduledDefaults.description,
            incidentPriority: initial?.incidentPriority ?? meta.priority,
            incidentType: initial?.incidentType ?? meta.type,
            instructions: initial?.instructions ?? incidentDefaults.instructions,
            schedule: cloneSchedule(initial?.schedule ?? scheduleDefaults),
            existingId: initial?.existingId,
            existingName: initial?.existingName,
        };
    });

    const [validation, setValidationState] = useState<TriggerValidationState>({});
    const [dirty, setDirty] = useState(defaultDirtyState);

    const setTrigger = useCallback((updater: (prev: TriggerState) => TriggerState) => {
        setTriggerState(prev => updater(prev));
    }, []);

    const setValidation = useCallback((updater: (prev: TriggerValidationState) => TriggerValidationState) => {
        setValidationState(prev => updater(prev));
    }, []);

    const updateDirtyFlags = useCallback((fields?: TriggerDirtyField[], reset = false) => {
        if (!fields && !reset) {
            return;
        }
        setDirty(prev => {
            if (reset) {
                return {
                    name: false,
                    description: false,
                    instructions: false,
                    schedule: false,
                };
            }
            if (!fields || fields.length === 0) {
                return prev;
            }
            const next = { ...prev };
            fields.forEach(field => {
                next[field] = true;
            });
            return next;
        });
    }, []);

    const updateFromUser = useCallback(
        (patch: TriggerUserPatch, fields?: TriggerDirtyField[]) => {
            if (fields && fields.length > 0) {
                updateDirtyFlags(fields);
            }
            setTriggerState(prev => {
                const { schedule: schedulePatch, ...rest } = patch;
                const mergedSchedule = schedulePatch ? ({ ...prev.schedule, ...schedulePatch } as TriggerScheduleState) : prev.schedule;
                return {
                    ...prev,
                    ...rest,
                    schedule: cloneSchedule(mergedSchedule),
                };
            });
        },
        [updateDirtyFlags]
    );

    const applyAgentDefaults = useCallback(
        (agentName?: string, agentDisplayName?: string) => {
            const resolvedDisplayName = agentDisplayName?.trim() || agentName?.trim() || undefined;
            const incidentAuto = getIncidentDefaults(intl, resolvedDisplayName);
            const scheduledAuto = refreshScheduledDefaults(intl, resolvedDisplayName);
            const scheduleAuto: TriggerScheduleState = {
                preset: DEFAULT_SCHEDULE_PRESET,
                cronExpression: SCHEDULE_PRESETS[DEFAULT_SCHEDULE_PRESET].cron,
                naturalText: '',
                timezone: trigger.schedule.timezone,
                startTime: trigger.schedule.startTime,
            };

            setTriggerState(prev => {
                const next: TriggerState = {
                    ...prev,
                    agentName,
                    agentDisplayName: resolvedDisplayName,
                };

                if (!dirty.name) {
                    next.name = prev.mode === 'scheduled' ? scheduledAuto.name : incidentAuto.name;
                }
                if (!dirty.description) {
                    next.description = scheduledAuto.description;
                }
                if (!dirty.instructions) {
                    next.instructions = prev.mode === 'scheduled' ? scheduledAuto.instructions : incidentAuto.instructions;
                }
                if (!dirty.schedule) {
                    next.schedule = cloneSchedule(scheduleAuto);
                }
                return next;
            });
        },
        [dirty, intl, trigger.schedule.startTime, trigger.schedule.timezone]
    );

    const reset = useCallback(
        (overrides?: Partial<TriggerState>) => {
            const defaults = buildTriggerDefaults(intl, overrides?.agentDisplayName ?? overrides?.agentName, incidentPlatformType);
            const incidentAuto = getIncidentDefaults(intl, overrides?.agentDisplayName ?? overrides?.agentName);
            const scheduledAuto = refreshScheduledDefaults(intl, overrides?.agentDisplayName ?? overrides?.agentName);

            setTriggerState({
                mode: overrides?.mode ?? defaults.mode,
                strategy: overrides?.strategy ?? defaults.strategy,
                agentName: overrides?.agentName,
                agentDisplayName: overrides?.agentDisplayName,
                name: overrides?.name ?? (overrides?.mode === 'scheduled' ? scheduledAuto.name : incidentAuto.name),
                description: overrides?.description ?? scheduledAuto.description,
                incidentPriority: overrides?.incidentPriority ?? defaults.incidentPriority,
                incidentType: overrides?.incidentType ?? defaults.incidentType,
                instructions:
                    overrides?.instructions ?? (overrides?.mode === 'scheduled' ? scheduledAuto.instructions : incidentAuto.instructions),
                schedule: cloneSchedule(overrides?.schedule ?? defaults.schedule),
                existingId: overrides?.existingId,
                existingName: overrides?.existingName,
            });
            setValidationState({});
            updateDirtyFlags(undefined, true);
        },
        [intl, updateDirtyFlags, incidentPlatformType]
    );

    return {
        trigger,
        validation,
        setTrigger,
        setValidation,
        applyAgentDefaults,
        reset,
        updateFromUser,
    };
};
