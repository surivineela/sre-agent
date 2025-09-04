import { Button, Image, makeStyles, mergeClasses, Text, tokens } from '@fluentui/react-components';
import { Sparkle16Filled } from '@fluentui/react-icons';
import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { useAzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { resolveResourceIcon } from '../../Common/Helpers/Resources';
import { KnowledgeGraphBuildStatusContext } from '../../Common/Providers/KnowledgeGraphBuildStatusProvider';
import { SreAgentResources } from '../../Strings/SREAgentResources';

const useChatSuggestionStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        gap: '20px',
        justifyContent: 'center',
        alignItems: 'center',
        flex: '1',
    },
    brandContainer: {
        display: 'flex',
        flexDirection: 'row',
        justifyContent: 'center',
        alignItems: 'center',
        gap: '8px',
    },
    cardContainer: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: '10px',
        maxWidth: '1000px',
        justifyContent: 'center',
        alignItems: 'center',
    },
    questionContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: '8px',
        maxWidth: '100%',
        width: '100%',
        alignItems: 'flex-start',
    },
    questionContainerGrouped: {
        display: 'flex',
        flexDirection: 'column',
        gap: '8px',
        width: '100%',
        alignItems: 'flex-start',
    },
    card: {
        width: '185px',
        height: '72px',
    },
    buttonUnclicked: {
        maxWidth: 'fit-content',
        minWidth: '20px',
        padding: '5px 16px',
        borderRadius: '16px',
        color: tokens.colorNeutralForeground3,
    },
    buttonClicked: {
        maxWidth: 'fit-content',
        minWidth: '20px',
        padding: '5px 16px',
        borderRadius: '16px',
        backgroundColor: tokens.colorNeutralStroke1,
        color: tokens.colorNeutralForeground3,
        '&:hover': {
            backgroundColor: `${tokens.colorNeutralStroke1} !important`,
            color: `${tokens.colorNeutralForeground3} !important`,
        },
        '&:active': {
            backgroundColor: `${tokens.colorNeutralStroke1} !important`,
            color: `${tokens.colorNeutralForeground3} !important`,
        },
        '&:focus': {
            backgroundColor: tokens.colorNeutralStroke1,
            color: tokens.colorNeutralForeground3,
        },
    },
    questionButton: {
        width: '100%',
        padding: '12px 16px',
        justifyContent: 'flex-start',
        textAlign: 'left',
        border: 'none',
        borderBottom: `1px solid ${tokens.colorNeutralBackground3}`,
        backgroundColor: 'transparent',
        minHeight: '40px',
    },
    leftRoot: {
        alignItems: 'flex-start',
        justifyContent: 'flex-start',
        width: '100%',
    },
    leftBrandContainer: {
        justifyContent: 'flex-start',
    },
    leftCardContainer: {
        justifyContent: 'flex-start',
        alignItems: 'flex-start',
        width: '100%',
    },
});

interface ChatSuggestionsProps {
    sendMessage: (message: string) => void | Promise<void>;
    categories?: string[];
    getQuestionsForCategory?: (category: string) => string[];
    showSreAgentLogo?: boolean;
    alignLeft?: boolean;
    getCategorySubcategories?: (category: string) => Record<string, string[]> | null;
    initialExpandedCategory?: string;
}

export const ChatSuggestions = (props: ChatSuggestionsProps) => {
    const {
        sendMessage,
        categories: categoriesProp,
        getQuestionsForCategory: externalGetQuestions,
        showSreAgentLogo = true,
        alignLeft = false,
        getCategorySubcategories,
        initialExpandedCategory,
    } = props;
    if (!sendMessage) {
        throw new Error('sendMessage prop is required');
    }

    const intl = useIntl();
    const chatSuggestionsStyles = useChatSuggestionStyles();
    const [clickedKey, setClickedKey] = useState<string>(initialExpandedCategory ?? '');

    const { hasChatPermissions } = useContext(KnowledgeGraphBuildStatusContext);
    const { logAmplitudeControlEvent } = useAzPortalContext();

    const defaultCategories = useMemo(() => ['About me', 'App Services', 'Container Apps', 'AKS', 'APIM'], []);

    const chatSuggestionCategories = useMemo(() => categoriesProp ?? defaultCategories, [categoriesProp, defaultCategories]);

    useEffect(() => {
        if (clickedKey && !chatSuggestionCategories.includes(clickedKey)) {
            setClickedKey('');
        }
    }, [chatSuggestionCategories, clickedKey]);

    // Map each category to an icon (Sparkle for About/Get started; resource icons for others)
    const iconMap: Record<string, JSX.Element> = useMemo(() => {
        const resourceImg = (azureType: string) => <img src={resolveResourceIcon(azureType)} alt="" style={{ width: 16, height: 16 }} />;
        return {
            'Get started': <Sparkle16Filled />,
            'About me': <Sparkle16Filled />,
            'App Services': resourceImg('microsoft.web/sites'),
            'Azure App Service': resourceImg('microsoft.web/sites'),
            'Container Apps': resourceImg('microsoft.app/containerapps'),
            'Azure Container App': resourceImg('microsoft.app/containerapps'),
            AKS: resourceImg('microsoft.containerservice/managedclusters'),
            'Azure Kubernetes Service': resourceImg('microsoft.containerservice/managedclusters'),
            APIM: resourceImg('microsoft.apimanagement/service'),
            'Azure API Management': resourceImg('microsoft.apimanagement/service'),
        };
    }, []);

    const AboutMeQuestions = useMemo(
        () => [
            'How do I get started with SRE Agent?',
            'What can you help me with as an SRE Agent?',
            'What are some common use cases you support?',
            'What are your key capabilities?',
            'Can you explain how you help with incident management?',
            'How do I connect to an Incident Platform?',
            'How does SRE Agent’s billing work?',
            'Which Azure services do you support?',
        ],
        []
    );

    const AppServicesQuestions = useMemo(
        () => [
            'List all my web apps',
            'What services or resources is my web app connected to?',
            'Which apps are hosted on Linux vs Windows in my environment?',
            'Are any of my web apps still running on deprecated or unsupported runtime versions?',
            'Show me visualization of memory usage % for my web app for last week',
            "Can you analyze my app's availability over the last 24 hours?",
            'Give me slow endpoints for my APIs',
            'Why is my web app timed out?',
            'Why is my web app throwing 500s?',
        ],
        []
    );

    const ContainerAppsQuestions = useMemo(
        () => [
            'List all my container apps',
            'What is the ingress configuration for my container app?',
            'Which revision of my container app is currently active?',
            'What changed in my container app in the last week?',
            'Show me visualization of memory utilization % for my container app for last week',
            'My container app is stuck in activation failed state',
            'Why is my container app timed out?',
            'Why is my container app throwing 500s?',
        ],
        []
    );

    const AKSQuestions = useMemo(
        () => [
            'Which node pools are configured for my AKS cluster?',
            'Which workloads are in a crash loop or failed state?',
            'Do I have any pending or unscheduled pods?',
            'Can you change settings on the cluster?',
            'Scale out deployment inside my AKS cluster',
            'Is there an OOM in my deployment?',
            'Analyze requests and limits in my namespace',
            'Why is my deployment down?',
        ],
        []
    );

    const APIMQuestions = useMemo(
        () => [
            'Can you show me my API Management instances?',
            'I need details about my specific API Management instance',
            'What backends does my API Management instance have?',
            'Does my API Management instance have any unhealthy backend apps?',
            'Why am I getting 500 errors in my API Management?',
            'Can you help me figure out why requests to our API are failing?',
            'Show me recent changes to our API Management instance',
            'Why is my API Management slow?',
            'Can you help me scale my API Management instance?',
        ],
        []
    );

    const defaultGetQuestions = useCallback(
        (category: string): string[] => {
            switch (category) {
                case 'About me':
                    return AboutMeQuestions;
                case 'App Services':
                    return AppServicesQuestions;
                case 'Container Apps':
                    return ContainerAppsQuestions;
                case 'AKS':
                    return AKSQuestions;
                case 'APIM':
                    return APIMQuestions;
                default:
                    return [];
            }
        },
        [AboutMeQuestions, AppServicesQuestions, ContainerAppsQuestions, AKSQuestions, APIMQuestions]
    );

    const getQuestionsForCategory = externalGetQuestions ?? defaultGetQuestions;

    const handleCategoryClick = (category: string) => {
        setClickedKey(prev => (prev === category ? '' : category));
    };

    const handleQuestionClick = (question: string) => {
        void sendMessage(question);
        setClickedKey('');
        logAmplitudeControlEvent({
            targetType: 'button',
            targetAction: 'clicked',
            targetName: 'newChatPromptSuggestion',
            targetFriendlyName: 'New chat prompt suggestion',
            valueObjectName: question,
            valueObjectFriendlyName: question,
        });
    };

    return (
        <div className={mergeClasses(chatSuggestionsStyles.root, alignLeft && chatSuggestionsStyles.leftRoot)}>
            {showSreAgentLogo && (
                <div className={mergeClasses(chatSuggestionsStyles.brandContainer, alignLeft && chatSuggestionsStyles.leftBrandContainer)}>
                    <Image src="./SreAgent.svg" width={32} height={32} alt={intl.formatMessage(SreAgentResources.sreAgent)} />
                    <Text size={500} weight="semibold">
                        {intl.formatMessage(SreAgentResources.sreAgent)}
                    </Text>
                </div>
            )}
            <div className={mergeClasses(chatSuggestionsStyles.cardContainer, alignLeft && chatSuggestionsStyles.leftCardContainer)}>
                {hasChatPermissions &&
                    chatSuggestionCategories.map(suggestion => (
                        <Button
                            key={suggestion}
                            onClick={() => handleCategoryClick(suggestion)}
                            appearance={clickedKey === suggestion ? 'primary' : 'secondary'}
                            className={
                                clickedKey === suggestion ? chatSuggestionsStyles.buttonClicked : chatSuggestionsStyles.buttonUnclicked
                            }
                            icon={iconMap[suggestion]}
                        >
                            <Text size={200} weight={'medium'}>
                                {suggestion}
                            </Text>
                        </Button>
                    ))}
            </div>
            {hasChatPermissions &&
                clickedKey &&
                (() => {
                    const rawGrouped = getCategorySubcategories?.(clickedKey);
                    const hasNonEmptySubcat = !!rawGrouped && Object.keys(rawGrouped).some(k => k.trim().length > 0);
                    const grouped = hasNonEmptySubcat ? rawGrouped : null;
                    const isGrouped = !!grouped && Object.keys(grouped).length > 0;
                    const containerClass = isGrouped
                        ? chatSuggestionsStyles.questionContainerGrouped
                        : chatSuggestionsStyles.questionContainer;
                    return (
                        <div className={containerClass}>
                            {isGrouped
                                ? Object.entries(grouped as Record<string, string[]>)
                                      .filter(([subcat]) => subcat.trim().length > 0)
                                      .map(([subcat, questions]) => (
                                          <div key={subcat} style={{ width: '100%', paddingBottom: '15px' }}>
                                              <Text
                                                  size={200}
                                                  weight="semibold"
                                                  style={{ color: tokens.colorNeutralForeground3, paddingLeft: '16px' }}
                                              >
                                                  {subcat}
                                              </Text>
                                              {questions.map(question => (
                                                  <Button
                                                      key={question}
                                                      onClick={() => handleQuestionClick(question)}
                                                      appearance={'subtle'}
                                                      className={chatSuggestionsStyles.questionButton}
                                                  >
                                                      <Text size={200} style={{ textAlign: 'left', width: '100%' }}>
                                                          {question}
                                                      </Text>
                                                  </Button>
                                              ))}
                                          </div>
                                      ))
                                : getQuestionsForCategory(clickedKey).map(question => (
                                      <Button
                                          key={question}
                                          onClick={() => handleQuestionClick(question)}
                                          appearance={'subtle'}
                                          className={chatSuggestionsStyles.questionButton}
                                      >
                                          <Text size={200} style={{ textAlign: 'left', width: '100%' }}>
                                              {question}
                                          </Text>
                                      </Button>
                                  ))}
                        </div>
                    );
                })()}
        </div>
    );
};
