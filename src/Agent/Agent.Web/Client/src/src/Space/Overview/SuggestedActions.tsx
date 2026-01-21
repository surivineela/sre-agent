import { Body1Strong } from '@fluentui-copilot/react-copilot';
import { Accordion, AccordionHeader, AccordionItem, AccordionPanel, Button, Card } from '@fluentui/react-components';
import { ChevronDownUpRegular, ChevronUpDownRegular, DismissRegular, RocketRegular } from '@fluentui/react-icons';
import { FC, useState } from 'react';
import { useIntl } from 'react-intl';
import { OverviewResources, SreAgentResources } from '../../Strings/SREAgentResources';

const SUGGESTED_ACTIONS_VALUE = 'suggestedActions';

const SuggestedActions: FC = () => {
    const [isOpen, setOpen] = useState<boolean>(true);

    const intl = useIntl();

    return (
        <Card size={'small'}>
            <Accordion collapsible openItems={isOpen ? [SUGGESTED_ACTIONS_VALUE] : []} onToggle={(_e, _data) => setOpen(!isOpen)}>
                <AccordionItem value={SUGGESTED_ACTIONS_VALUE}>
                    <AccordionHeader
                        expandIconPosition={'end'}
                        expandIcon={
                            <>
                                <Button
                                    size={'small'}
                                    appearance="transparent"
                                    icon={isOpen ? <ChevronDownUpRegular /> : <ChevronUpDownRegular />}
                                >
                                    {isOpen ? intl.formatMessage(SreAgentResources.collapse) : intl.formatMessage(SreAgentResources.expand)}
                                </Button>
                                <Button
                                    size={'small'}
                                    appearance="transparent"
                                    icon={<RocketRegular />}
                                    onClick={e => {
                                        e.preventDefault();
                                    }}
                                >
                                    {intl.formatMessage(OverviewResources.goToQuickStart)}
                                </Button>
                                <Button
                                    size={'small'}
                                    appearance="transparent"
                                    icon={<DismissRegular />}
                                    onClick={e => {
                                        e.preventDefault();
                                    }}
                                >
                                    {intl.formatMessage(SreAgentResources.dismiss)}
                                </Button>
                            </>
                        }
                    >
                        <Body1Strong>{intl.formatMessage(OverviewResources.suggestionActions, { value: 1 })}</Body1Strong>
                    </AccordionHeader>
                    <AccordionPanel></AccordionPanel>
                </AccordionItem>
            </Accordion>
        </Card>
    );
};

export default SuggestedActions;
