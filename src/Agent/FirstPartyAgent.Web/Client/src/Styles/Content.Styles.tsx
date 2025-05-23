import { mergeStyleSets,IStyle, mergeStyles } from "@fluentui/merge-styles";


export const ContentStyleSets = mergeStyleSets({
    container: {
        marginTop: "100px",
    }
});

export const ItemPaddingStyles = mergeStyles({
    paddingBottom: "10px",
});

export const PanelStyles = mergeStyleSets({
    container: {
        marginTop: "16px",
    }
});