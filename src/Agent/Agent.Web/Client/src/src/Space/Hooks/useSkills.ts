import { useCallback, useContext, useState } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ExtendedAgentClient } from '../../Common/Clients/ExtendedAgentClient';
import { Skill } from '../Contracts/ExtendedAgentGraph';

export const useSkills = () => {
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const [skills, setSkills] = useState<Skill[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const fetchSkills = useCallback(async () => {
        setLoading(true);
        setError(null);
        const client = ExtendedAgentClient.getInstance(sreAgentEndpoint);
        const response = await client.getSkills();
        if (response.isSuccessful && response.content) {
            setSkills(response.content);
        } else {
            setError('Failed to fetch skills');
            setSkills([]);
        }
        setLoading(false);
    }, [sreAgentEndpoint]);

    const createSkill = useCallback(
        async (skill: Skill) => {
            const client = ExtendedAgentClient.getInstance(sreAgentEndpoint);
            const response = await client.createOrUpdateSkill(skill);
            if (response.isSuccessful) {
                await fetchSkills();
                return { success: true };
            } else {
                return { success: false, error: response.error };
            }
        },
        [sreAgentEndpoint, fetchSkills]
    );

    const updateSkill = useCallback(
        async (skill: Skill) => {
            const client = ExtendedAgentClient.getInstance(sreAgentEndpoint);
            const response = await client.createOrUpdateSkill(skill);
            if (response.isSuccessful) {
                await fetchSkills();
                return { success: true };
            } else {
                return { success: false, error: response.error };
            }
        },
        [sreAgentEndpoint, fetchSkills]
    );

    const deleteSkill = useCallback(
        async (skillName: string) => {
            const client = ExtendedAgentClient.getInstance(sreAgentEndpoint);
            const response = await client.deleteSkill(skillName);
            if (response.isSuccessful) {
                await fetchSkills();
                return { success: true };
            } else {
                return { success: false, error: response.error };
            }
        },
        [sreAgentEndpoint, fetchSkills]
    );

    return {
        skills,
        loading,
        error,
        fetchSkills,
        createSkill,
        updateSkill,
        deleteSkill,
    };
};
