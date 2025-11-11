export interface TeamsChannelInfo {
    channelId: string;
    teamsGroupId: string;
}

/**
 * Parses a Teams channel link to extract the channel ID and teams group ID
 * Example: https://teams.microsoft.com/l/channel/19%3AofK0U-rC4QzuHJnhY-hsXC_u6r75ojhFm90zCa_7glI1%40thread.tacv2/test%20connection?groupId=33d2c4cf-7179-4749-a161-488a58eb4234
 * Returns: { channelId: "19:ofK0U-rC4QzuHJnhY-hsXC_u6r75ojhFm90zCa_7glI1@thread.tacv2", teamsGroupId: "33d2c4cf-7179-4749-a161-488a58eb4234" }
 */
export const parseTeamsChannelLink = (teamsChannelLink: string): TeamsChannelInfo | null => {
    try {
        const url = new URL(teamsChannelLink);

        // Extract channel ID from the path (second segment after /l/channel/)
        const pathSegments = url.pathname.split('/');
        const channelIndex = pathSegments.indexOf('channel');

        if (channelIndex === -1 || channelIndex + 1 >= pathSegments.length) {
            return null;
        }

        // Decode the URL-encoded channel ID
        const channelId = decodeURIComponent(pathSegments[channelIndex + 1]);

        // Extract teams group ID from query parameter
        const teamsGroupId = url.searchParams.get('groupId');

        if (!channelId || !teamsGroupId) {
            return null;
        }

        return {
            channelId,
            teamsGroupId,
        };
    } catch (error) {
        return null;
    }
};
