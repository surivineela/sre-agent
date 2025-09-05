import { useIncidentManagementStyles } from '../Styles/IncidentManagement.styles';

const ResponsePlan = () => {
    const styles = useIncidentManagementStyles();

    return (
        <div className={styles.navPanelWrapper}>
            <div className={styles.navPanelContent}>
                <div className={styles.navPanelPadding}>
                    <div>Response Plan for things and stuff</div>
                </div>
            </div>
        </div>
    );
};

export default ResponsePlan;
