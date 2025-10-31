import { Field, Radio, RadioGroup } from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { useMemo } from 'react';
import { useIntl } from 'react-intl';
import { TextWithLink } from '../../../Common/Components/TextWithLink';
import { LearnMoreLinks } from '../../../Common/Constants/Links';
import { AgentAccessLevel } from '../../../Common/Contracts/SreAgent';
import { PortalResources } from '../../../Strings/Resources';
import { SreAgentCreateFormProps } from './CreateAgentDialog';
import PermissionsGrid from './PermissionsGrid';

interface AgentPermissionsProps {
    isDeploying: boolean;
}

export const AgentPermissions = ({ isDeploying }: AgentPermissionsProps) => {
    const intl = useIntl();
    const { values, setFieldValue, errors } = useFormikContext<SreAgentCreateFormProps>();

    const permissionsLevelOptions = useMemo(() => {
        return [
            {
                key: AgentAccessLevel.low,
                text: intl.formatMessage(PortalResources.reader),
                onRenderLabel: () => (
                    <div>
                        <div>{intl.formatMessage(PortalResources.reader)}</div>
                        <div>{intl.formatMessage(PortalResources.readerDescription)}</div>
                    </div>
                ),
            },
            {
                key: AgentAccessLevel.high,
                text: intl.formatMessage(PortalResources.privileged),
                onRenderLabel: () => (
                    <div>
                        <div>{intl.formatMessage(PortalResources.privileged)}</div>
                        <div>{intl.formatMessage(PortalResources.privilegedDescription)}</div>
                    </div>
                ),
            },
        ];
    }, [intl]);

    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
            <TextWithLink
                text={intl.formatMessage(PortalResources.permissionsDescription)}
                linkText={intl.formatMessage(PortalResources.learnMoreAboutAgentPermissions)}
                linkUrl={LearnMoreLinks.sreAgentAgentPermissions}
            />

            <Field
                label={intl.formatMessage(PortalResources.permissionsLevel)}
                validationMessage={errors.permissionsLevel}
                validationState={errors.permissionsLevel ? 'error' : undefined}
            >
                <RadioGroup
                    value={values.permissionsLevel}
                    onChange={(_e, data) => setFieldValue('permissionsLevel', data.value)}
                    disabled={isDeploying}
                >
                    {permissionsLevelOptions.map(option => (
                        <Radio key={option.key} value={option.key} label={option.onRenderLabel ? option.onRenderLabel() : option.text} />
                    ))}
                </RadioGroup>
            </Field>

            <PermissionsGrid />
        </div>
    );
};
