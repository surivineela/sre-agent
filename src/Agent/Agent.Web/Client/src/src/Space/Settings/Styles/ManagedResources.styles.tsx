import { makeStyles, tokens } from '@fluentui/react-components';
import { IDetailsListStyles } from '@fluentui/react/lib/DetailsList';

export const useManagedResourcesStyles = makeStyles({
    pillsContainer: { display: 'flex', flexDirection: 'row', gap: '5px' },
    buttonStyle: { width: 'fit-content' },
    buttonsContainer: { display: 'flex', flexDirection: 'row', gap: '10px' },
    container: { display: 'flex', flexDirection: 'column', gap: '13px' },
    pickerItem: {
        flex: 1,
        maxWidth: '33.33%',
        marginLeft: '5px',
    },
    pickerRow: {
        display: 'flex',
        width: '100%',
        flexDirection: 'row',
        gap: '5px',
        paddingTop: '5px',
    },
    row: {
        display: 'flex',
        alignItems: 'center',
    },
    statusRow: {
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
    },
    icon: {
        width: '16px',
        height: '16px',
        paddingTop: '2px',
        paddingRight: '2px',
    },
    dialogFooter: {
        display: 'flex',
        flexShrink: 0,
        position: 'sticky',
        bottom: 0,
    },
    dialogContent: {
        display: 'flex',
        flexDirection: 'column',
        width: '100%',
        overflowY: 'hidden',
    },
    dialog: {
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        gap: '10px',
        overflowY: 'hidden',
    },
    itemDescription: {
        fontSize: '13px',
        lineHeight: '18px',
        margin: '0',
        marginBlock: '0',
    },
    itemTitle: {
        fontSize: '14px',
        fontWeight: '600',
        lineHeight: '20px',
        margin: '0',
        marginBlock: '0',
    },
    dropdownWithPadding: {
        minWidth: '345px',
        maxWidth: '545px',
        width: '100%',
        paddingBottom: '10px',
        paddingTop: '5px',
    },
    dropdown: {
        minWidth: '345px',
        maxWidth: '545px',
        width: '100%',
    },
    fieldPadding: {
        paddingBottom: '10px',
        paddingTop: '5px',
    },
    formField: {
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'flex-start',
        alignItems: 'flex-start',
        gap: '10px',
    },
    root: {
        marginTop: '10px',
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'flex-start',
        alignItems: 'flex-start',
        gap: '15px',
    },
    footerButtonDiv: {
        marginRight: '5px',
    },
    iconRow: {
        display: 'flex',
        flexDirection: 'row',
        justifyContent: 'flex-start',
        alignItems: 'center',
        gap: '4px',
    },
    dangerButton: {
        backgroundColor: tokens.colorStatusDangerBackground3,
        color: tokens.colorNeutralBackground1,
    },
    header: { 
        fontSize: '18px',
        fontWeight: 600,
    },
    searchBox: { 
        width: '330px',
        fontSize: '13px',
        zIndex: 1,
    },
    detailsList: {
        paddingTop: '0px',
        marginTop: '-16px'
    },
});

export const detailsListStyles: Partial<IDetailsListStyles> = {
    root: {
        width: '100%',
        maxHeight: '365px',
        overflowX: 'hidden',
    },
};
