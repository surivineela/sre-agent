import { makeStyles, tokens } from '@fluentui/react-components';

export const useTraceBadgeStyles = makeStyles({
    incident: {
        color: tokens.colorPaletteCranberryForeground2,
        backgroundColor: tokens.colorPaletteCranberryBackground2,
    },
    agent: {
        color: tokens.colorPaletteBlueForeground2,
        backgroundColor: tokens.colorPaletteBlueBackground2,
    },
    agentResponse: {
        color: tokens.colorPaletteSeafoamForeground2,
        backgroundColor: tokens.colorPaletteSeafoamBackground2,
    },
    agentHandoff: {
        color: tokens.colorPaletteMarigoldForeground2,
        backgroundColor: tokens.colorPaletteMarigoldBackground2,
    },
    modelGeneration: {
        color: tokens.colorPalettePinkForeground2,
        backgroundColor: tokens.colorPalettePinkBackground2,
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
