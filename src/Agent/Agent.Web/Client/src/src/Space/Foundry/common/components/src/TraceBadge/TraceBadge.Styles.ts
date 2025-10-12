import { makeStyles, tokens } from '@fluentui/react-components';

export const useTraceBadgeStyles = makeStyles({
    incident: {
        color: tokens.colorPaletteCranberryForeground2,
        backgroundColor: tokens.colorPaletteCranberryBackground2,
    },
    agent: {
        color: tokens.colorPaletteCornflowerForeground2,
        backgroundColor: tokens.colorPaletteCornflowerBackground2,
    },
    agentResponse: {
        color: tokens.colorPaletteNavyForeground2,
        backgroundColor: tokens.colorPaletteNavyBackground2,
    },
    agentHandoff: {
        color: tokens.colorPaletteGoldForeground2,
        backgroundColor: tokens.colorPaletteGoldBackground2,
    },
    modelGeneration: {
        color: tokens.colorPaletteRoyalBlueForeground2,
        backgroundColor: tokens.colorPaletteRoyalBlueBackground2,
    },
    subagent: {
        color: tokens.colorPaletteLavenderForeground2,
        backgroundColor: tokens.colorPaletteLavenderBackground2,
    },
    tool: {
        color: tokens.colorPaletteLilacForeground2,
        backgroundColor: tokens.colorPaletteLilacBackground2,
    },
    user: {
        color: tokens.colorPaletteLightTealForeground2,
        backgroundColor: tokens.colorPaletteLightTealBackground2,
    },
});
