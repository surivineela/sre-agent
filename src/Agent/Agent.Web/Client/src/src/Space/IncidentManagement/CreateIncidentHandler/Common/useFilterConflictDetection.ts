import { useMemo } from 'react';
import { IncidentFilter, IncidentTriggerEvent } from '../../../../Common/Contracts/Azure/IncidentHandler';
import { FilterConflictInfo } from './FilterConflictWarning';

interface UseFilterConflictDetectionParams {
    currentFilterId?: string;
    currentOwningTeamId?: string;
    currentIncidentType?: string;
    currentImpactedService?: string;
    currentPriority?: string;
    currentTriggers: IncidentTriggerEvent[];
    existingFilters: IncidentFilter[];
}

/**
 * Hook to detect potential overlapping filters that might process the same incidents.
 * This helps users avoid creating duplicate triggers for the same incident patterns.
 */
export const useFilterConflictDetection = ({
    currentFilterId,
    currentOwningTeamId,
    currentIncidentType,
    currentImpactedService,
    currentPriority,
    currentTriggers,
    existingFilters,
}: UseFilterConflictDetectionParams): FilterConflictInfo[] => {
    return useMemo(() => {
        const conflicts: FilterConflictInfo[] = [];

        for (const filter of existingFilters) {
            // Skip self-comparison when editing
            if (filter.id === currentFilterId) {
                continue;
            }

            // Skip disabled filters
            if (!filter.isEnabled || filter.isDeleted) {
                continue;
            }

            // Check if filters could match the same incidents
            const couldMatchSameIncidents = checkFilterOverlap(
                {
                    owningTeamId: currentOwningTeamId,
                    incidentType: currentIncidentType,
                    impactedService: currentImpactedService,
                    priority: currentPriority,
                },
                {
                    owningTeamId: filter.owningTeamId,
                    incidentType: filter.incidentType,
                    impactedService: filter.impactedService,
                    priority: filter.priority,
                }
            );

            if (!couldMatchSameIncidents) {
                continue;
            }

            // Check for overlapping triggers
            const existingTriggers = filter.triggers || [IncidentTriggerEvent.IncidentCreatedOrTransferred];
            const overlappingTriggers = currentTriggers.filter(t => existingTriggers.includes(t));

            if (overlappingTriggers.length > 0) {
                conflicts.push({
                    filterName: filter.id, // Use ID as name since IncidentFilter doesn't have a name field
                    filterId: filter.id,
                    overlappingTriggers: overlappingTriggers.map(getTriggerDisplayName),
                });
            }
        }

        return conflicts;
    }, [
        currentFilterId,
        currentOwningTeamId,
        currentIncidentType,
        currentImpactedService,
        currentPriority,
        currentTriggers,
        existingFilters,
    ]);
};

interface FilterCriteria {
    owningTeamId?: string;
    incidentType?: string;
    impactedService?: string;
    priority?: string;
}

/**
 * Check if two filter criteria could potentially match the same incidents.
 * Returns true if there's any overlap (either filter uses "ALL" or they have the same value).
 */
const checkFilterOverlap = (current: FilterCriteria, existing: FilterCriteria): boolean => {
    // Check owning team (if both specified, they must match)
    if (current.owningTeamId && existing.owningTeamId && current.owningTeamId !== existing.owningTeamId) {
        return false;
    }

    // Check incident type (ALL matches everything)
    if (!criteriaOverlaps(current.incidentType, existing.incidentType)) {
        return false;
    }

    // Check impacted service (ALL matches everything)
    if (!criteriaOverlaps(current.impactedService, existing.impactedService)) {
        return false;
    }

    // Check priority (ALL matches everything)
    if (!criteriaOverlaps(current.priority, existing.priority)) {
        return false;
    }

    return true;
};

/**
 * Check if two criteria values overlap.
 * "ALL" or empty string matches everything.
 */
const criteriaOverlaps = (value1?: string, value2?: string): boolean => {
    const isAll1 = !value1 || value1 === 'ALL' || value1 === '';
    const isAll2 = !value2 || value2 === 'ALL' || value2 === '';

    // If either is ALL, they overlap
    if (isAll1 || isAll2) {
        return true;
    }

    // Both are specific values, check if they match
    return value1 === value2;
};

/**
 * Get a human-readable display name for a trigger event.
 */
const getTriggerDisplayName = (trigger: IncidentTriggerEvent): string => {
    switch (trigger) {
        case IncidentTriggerEvent.IncidentCreatedOrTransferred:
            return 'Created/Transferred';
        case IncidentTriggerEvent.DiscussionEntry:
            return 'Discussion Entry';
        case IncidentTriggerEvent.IncidentMitigated:
            return 'Mitigated';
        case IncidentTriggerEvent.IncidentReactivated:
            return 'Reactivated';
        case IncidentTriggerEvent.IncidentResolved:
            return 'Resolved';
        default:
            return trigger;
    }
};
