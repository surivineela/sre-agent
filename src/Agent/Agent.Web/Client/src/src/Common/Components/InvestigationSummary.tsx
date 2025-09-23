import { Checkmark24Regular, ChevronDown24Regular, ChevronUp24Regular, Document24Regular } from '@fluentui/react-icons';
import React, { useEffect, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { SreAgentResources } from '../../Strings/SREAgentResources';

// ────────────────────────────────────────────────────────────────────────────────
// TYPES
// ────────────────────────────────────────────────────────────────────────────────
interface SummaryItem {
    title: string;
    summary: string;
    isCollapsed: boolean;
    status?: 'loading' | 'completed' | 'error';
    isFinal?: boolean;
}

interface InvestigationSummaries {
    containerTitle: string;
    summaries: SummaryItem[];
}

interface Section {
    id: number;
    title: string;
    expanded: boolean;
    thinking: boolean;
    content: string;
    status: 'loading' | 'completed' | 'error';
}

export interface InvestigationSummaryPanelProps {
    messageText: string;
}

const InvestigationSummaryPanel: React.FC<InvestigationSummaryPanelProps> = ({ messageText }) => {
    const intl = useIntl();
    // ────────────────────────────────────────────────────────────────────────────
    // STATE
    // ────────────────────────────────────────────────────────────────────────────
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    // Animation / layout flags
    const [mainExpanded, setMainExpanded] = useState(true);
    const [thinking, setThinking] = useState(true);
    const [visibleSections, setVisibleSections] = useState<number[]>([]);
    const [currentShimmerSection, setCurrentShimmerSection] = useState(0);
    const [complete, setComplete] = useState(false);

    // Track if user manually opened/closed anything – will suppress auto-collapse
    const userInteractedRef = useRef(false);

    // Final summary
    const [finalSummaryVisible, setFinalSummaryVisible] = useState(false);
    const [finalSummaryText, setFinalSummaryText] = useState('');

    // Parsed sections
    const [sections, setSections] = useState<Section[]>([]);
    const sectionsRef = useRef<Section[]>([]);
    useEffect(() => {
        sectionsRef.current = sections;
    }, [sections]);

    const [containerTitle, setContainerTitle] = useState(intl.formatMessage(SreAgentResources.investigationStartingHypothesis));

    // ────────────────────────────────────────────────────────────────────────────
    // EFFECT: PARSE messageText
    // ────────────────────────────────────────────────────────────────────────────
    useEffect(() => {
        setIsLoading(true);
        setError(null);

        // Helpers -------------------------------------------------------------
        const safeJsonParse = <T,>(raw: string): T | null => {
            try {
                // Pass 1 – try as-is
                const firstPass = JSON.parse(raw);
                // Some pipelines double-encode the JSON so the first parse may return a
                // string with escaped quotes.  If so – parse again.
                if (typeof firstPass === 'string') {
                    return JSON.parse(firstPass) as T;
                }
                return firstPass as T;
            } catch {
                return null;
            }
        };

        const parseLooseFormat = (text: string): void => {
            // Handle <final-summary>…</final-summary>
            const finalRegex = /<final-summary>([\s\S]*?)<\/final-summary>/i;
            const fMatch = text.match(finalRegex);
            if (fMatch?.[1]) setFinalSummaryText(fMatch[1].trim());

            const rawContent = text.replace(finalRegex, '').trim();
            setSections([
                {
                    id: 1,
                    title: intl.formatMessage(SreAgentResources.investigationResults),
                    expanded: true,
                    thinking: false,
                    content: rawContent,
                    status: 'completed',
                },
            ]);
        };

        try {
            if (!messageText) {
                setIsLoading(false);
                return;
            }

            const summariesRegex = /<investigation-summaries>([\s\S]*?)<\/investigation-summaries>/i;
            const match = messageText.match(summariesRegex);

            if (match?.[1]) {
                const rawJson = match[1].trim();
                const data = safeJsonParse<InvestigationSummaries>(rawJson);

                if (data) {
                    if (data.containerTitle) setContainerTitle(data.containerTitle);

                    const finalItem = data.summaries.find(s => s.isFinal);
                    if (finalItem) setFinalSummaryText(finalItem.summary);

                    const parsedSections: Section[] = data.summaries.map((s, idx) => ({
                        id: idx + 1,
                        title: s.title,
                        expanded: !(s.isCollapsed ?? true),
                        thinking: s.status === 'loading',
                        content: s.summary,
                        status: s.status ?? 'loading',
                    }));

                    setSections(parsedSections);
                } else {
                    // Invalid JSON – fall back to loose format so at least something shows up
                    setError('Failed to parse investigation data');
                    parseLooseFormat(messageText);
                }
            } else {
                parseLooseFormat(messageText);
            }
        } catch (err) {
            console.error('Error processing message text', err);
            setError('Failed to process investigation message');
            parseLooseFormat(messageText);
        } finally {
            setIsLoading(false);
        }
    }, [messageText, intl]);

    // ────────────────────────────────────────────────────────────────────────────
    // EFFECT: KICK-OFF animation once sections ready
    // ────────────────────────────────────────────────────────────────────────────
    useEffect(() => {
        if (sections.length) {
            startAnimation();
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [sections]);

    // ────────────────────────────────────────────────────────────────────────────
    // EFFECT: AUTO-COLLAPSE main container when everything is done & user
    // hasn’t interacted with it.
    // ────────────────────────────────────────────────────────────────────────────
    useEffect(() => {
        if (complete && !userInteractedRef.current) {
            setMainExpanded(false);
        }
    }, [complete]);

    // ────────────────────────────────────────────────────────────────────────────
    // ANIMATION helpers
    // ────────────────────────────────────────────────────────────────────────────
    const startAnimation = () => {
        setThinking(true);
        setComplete(false);
        setVisibleSections([]);
        setCurrentShimmerSection(0);
        setTimeout(showNextSection, 500);
    };

    const showNextSection = () => {
        const allSections = sectionsRef.current;

        // Finished ?
        if (visibleSections.length >= allSections.length) {
            setTimeout(() => {
                setCurrentShimmerSection(0);
                setThinking(false);
                setComplete(true);
                if (finalSummaryText) setFinalSummaryVisible(true);
            }, 800);
            return;
        }

        const nextIdx = visibleSections.length;
        const nextId = allSections[nextIdx].id;

        setCurrentShimmerSection(nextId);
        setVisibleSections(prev => [...prev, nextId]);
        setTimeout(showNextSection, 1200);
    };

    // ────────────────────────────────────────────────────────────────────────────
    // CLICK handlers
    // ────────────────────────────────────────────────────────────────────────────
    const toggleMainSection = () => {
        userInteractedRef.current = true;
        if (!thinking) setMainExpanded(e => !e);
    };

    const toggleSubSection = (id: number) => {
        userInteractedRef.current = true;
        if (!thinking) {
            setSections(prev => prev.map(s => (s.id === id ? { ...s, expanded: !s.expanded } : s)));
        }
    };

    // ────────────────────────────────────────────────────────────────────────────
    // RENDER helpers – skeleton & error short-circuits
    // ────────────────────────────────────────────────────────────────────────────
    if (isLoading) {
        return (
            <div style={{ width: '100%', marginBottom: 8 }}>
                <div
                    style={{
                        backgroundColor: '#f9f9f9',
                        borderRadius: 8,
                        padding: 12,
                        border: '1px solid #e0e0e0',
                        animation: 'pulse 1.5s infinite',
                    }}
                >
                    <div
                        style={{
                            height: 16,
                            width: '75%',
                            backgroundColor: '#e0e0e0',
                            borderRadius: 4,
                            marginBottom: 12,
                        }}
                    />
                    <div
                        style={{
                            height: 16,
                            width: '50%',
                            backgroundColor: '#e0e0e0',
                            borderRadius: 4,
                        }}
                    />
                </div>
            </div>
        );
    }

    if (error) {
        return (
            <div style={{ width: '100%', marginBottom: 8 }}>
                <div
                    style={{
                        backgroundColor: '#fff0f0',
                        borderRadius: 8,
                        padding: 12,
                        border: '1px solid #ffcccb',
                    }}
                >
                    <p
                        style={{
                            fontWeight: 'bold',
                            color: '#d32f2f',
                            margin: 0,
                            fontSize: 14,
                        }}
                    >
                        {intl.formatMessage(SreAgentResources.investigationErrorLoading)}
                    </p>
                    <p style={{ color: '#d32f2f', margin: 0, fontSize: 13 }}>{error}</p>
                </div>
            </div>
        );
    }

    // ────────────────────────────────────────────────────────────────────────────
    // MAIN RENDER
    // ────────────────────────────────────────────────────────────────────────────
    return (
        <div style={{ width: '100%', marginBottom: 16 }}>
            {/* ——————————————————— Main header */}
            <div
                style={{
                    width: '100%',
                    transition: 'all 600ms ease',
                    transform: !mainExpanded && complete ? 'scale(0.96)' : 'scale(1)',
                }}
            >
                <div
                    style={{
                        backgroundColor: '#f9f9f9',
                        padding: !mainExpanded && complete ? 8 : 12,
                        borderRadius: 8,
                        border: '1px solid #e0e0e0',
                        marginBottom: 4,
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'space-between',
                        cursor: 'pointer',
                        transition: 'all 300ms',
                    }}
                    onClick={toggleMainSection}
                >
                    <div style={{ display: 'flex', alignItems: 'center' }}>
                        <Document24Regular
                            style={{
                                color: '#666',
                                marginRight: 12,
                                width: 20,
                                height: 20,
                            }}
                        />
                        <div style={{ position: 'relative' }}>
                            <span
                                style={{
                                    fontWeight: 500,
                                    color: !mainExpanded && complete ? '#999' : '#333',
                                    fontSize: 14,
                                    transition: 'all 300ms',
                                }}
                            >
                                {containerTitle}
                            </span>
                            {thinking && currentShimmerSection === 0 && (
                                <div
                                    style={{
                                        position: 'absolute',
                                        inset: 0,
                                        overflow: 'hidden',
                                    }}
                                >
                                    <div className="shimmer-effect" />
                                </div>
                            )}
                        </div>
                    </div>
                    <div style={{ display: 'flex', alignItems: 'center' }}>
                        <Checkmark24Regular style={{ color: complete ? '#666' : '#ccc', marginRight: 8 }} />
                        {mainExpanded ? (
                            <ChevronUp24Regular style={{ color: '#666' }} />
                        ) : (
                            <ChevronDown24Regular style={{ color: '#666' }} />
                        )}
                    </div>
                </div>

                {/* ——————————————————— Sub-sections */}
                <div
                    style={{
                        overflow: 'hidden',
                        transition: 'all 600ms ease',
                        maxHeight: mainExpanded ? 1200 : 0,
                        opacity: mainExpanded ? 1 : 0,
                        transform: mainExpanded ? 'translateY(0)' : 'translateY(-8px)',
                    }}
                >
                    {mainExpanded && (
                        <div
                            style={{
                                backgroundColor: '#fff',
                                borderRadius: 8,
                                border: '1px solid #e0e0e0',
                                padding: 16,
                                marginTop: 8,
                            }}
                        >
                            {sections.map(section => {
                                // PLACEHOLDER while still animating
                                if (thinking && !visibleSections.includes(section.id) && section.id > visibleSections.length) {
                                    return (
                                        <div key={section.id} style={{ marginBottom: 12 }}>
                                            <div
                                                style={{
                                                    backgroundColor: '#f0f0f0',
                                                    borderRadius: 8,
                                                    padding: 12,
                                                    display: 'flex',
                                                    alignItems: 'center',
                                                    justifyContent: 'space-between',
                                                    opacity: 0.4,
                                                }}
                                            >
                                                <div style={{ display: 'flex', alignItems: 'center' }}>
                                                    <div
                                                        style={{
                                                            width: 20,
                                                            height: 20,
                                                            backgroundColor: '#e0e0e0',
                                                            borderRadius: 4,
                                                            marginRight: 8,
                                                        }}
                                                    />
                                                    <div
                                                        style={{
                                                            width: 220,
                                                            height: 16,
                                                            backgroundColor: '#e0e0e0',
                                                            borderRadius: 4,
                                                        }}
                                                    />
                                                </div>
                                                <div style={{ display: 'flex', alignItems: 'center' }}>
                                                    <div
                                                        style={{
                                                            width: 16,
                                                            height: 16,
                                                            backgroundColor: '#e0e0e0',
                                                            borderRadius: '50%',
                                                            marginRight: 8,
                                                        }}
                                                    />
                                                    <div
                                                        style={{
                                                            width: 20,
                                                            height: 20,
                                                            backgroundColor: '#e0e0e0',
                                                            borderRadius: 4,
                                                        }}
                                                    />
                                                </div>
                                            </div>
                                        </div>
                                    );
                                }

                                // VISIBLE section ------------------------------------------------
                                if (visibleSections.includes(section.id)) {
                                    return (
                                        <div
                                            key={section.id}
                                            style={{
                                                marginBottom: 12,
                                                animation: 'fadeIn 0.45s ease',
                                            }}
                                        >
                                            {/* Section header */}
                                            <div
                                                style={{
                                                    backgroundColor: '#f9f9f9',
                                                    border: '1px solid #e0e0e0',
                                                    borderRadius: 8,
                                                    padding: 12,
                                                    display: 'flex',
                                                    alignItems: 'center',
                                                    justifyContent: 'space-between',
                                                    cursor: 'pointer',
                                                    transition: 'all 200ms',
                                                }}
                                                onClick={() => toggleSubSection(section.id)}
                                            >
                                                <div style={{ display: 'flex', alignItems: 'center' }}>
                                                    <Document24Regular style={{ color: '#666', marginRight: 8 }} />
                                                    <div style={{ position: 'relative' }}>
                                                        <span style={{ color: '#333', fontSize: 13 }}>{section.title}</span>
                                                        {currentShimmerSection === section.id && (
                                                            <div
                                                                style={{
                                                                    position: 'absolute',
                                                                    inset: 0,
                                                                    overflow: 'hidden',
                                                                }}
                                                            >
                                                                <div className="shimmer-effect" />
                                                            </div>
                                                        )}
                                                    </div>
                                                </div>
                                                <div style={{ display: 'flex', alignItems: 'center' }}>
                                                    <Checkmark24Regular
                                                        style={{
                                                            color: section.status === 'completed' ? '#666' : '#e0e0e0',
                                                            marginRight: 8,
                                                        }}
                                                    />
                                                    {section.expanded ? (
                                                        <ChevronUp24Regular style={{ color: '#666' }} />
                                                    ) : (
                                                        <ChevronDown24Regular style={{ color: '#666' }} />
                                                    )}
                                                </div>
                                            </div>

                                            {/* Section content */}
                                            <div
                                                style={{
                                                    overflow: 'hidden',
                                                    transition: 'all 200ms',
                                                    maxHeight: section.expanded ? 600 : 0,
                                                    opacity: section.expanded ? 1 : 0,
                                                    marginTop: section.expanded ? 8 : 0,
                                                }}
                                            >
                                                {section.expanded && (
                                                    <div
                                                        style={{
                                                            backgroundColor: '#f9f9f9',
                                                            border: '1px solid #e0e0e0',
                                                            borderRadius: 8,
                                                            padding: 12,
                                                            marginLeft: 24,
                                                            color: '#333',
                                                            fontSize: 13,
                                                        }}
                                                        dangerouslySetInnerHTML={{
                                                            __html: section.content.replace(/\n/g, '<br>'),
                                                        }}
                                                    />
                                                )}
                                            </div>
                                        </div>
                                    );
                                }

                                return null; // not yet visible & no placeholder
                            })}
                        </div>
                    )}
                </div>
            </div>

            {/* ——————————————————— Final summary */}
            {finalSummaryVisible && finalSummaryText && (
                <div
                    style={{
                        width: '100%',
                        backgroundColor: '#f9f9f9',
                        border: '1px solid #e0e0e0',
                        borderRadius: 8,
                        padding: 16,
                        marginTop: 16,
                        animation: 'fadeIn 0.4s ease',
                    }}
                >
                    <div style={{ display: 'flex' }}>
                        <span
                            style={{
                                fontWeight: 500,
                                color: '#333',
                                marginRight: 8,
                                fontSize: 14,
                            }}
                        >
                            {intl.formatMessage(SreAgentResources.investigationFinalSummaryLabel)}
                        </span>
                        <div
                            style={{ color: '#555', fontSize: 13 }}
                            dangerouslySetInnerHTML={{
                                __html: finalSummaryText.replace(/\n/g, '<br>'),
                            }}
                        />
                    </div>
                </div>
            )}

            {/* ——————————————————— Inline CSS for shimmer / animations */}
            <style>
                {`
          @keyframes shimmer {
            0% { transform: translateX(-100%); }
            100% { transform: translateX(100%); }
          }
          .shimmer-effect {
            position: absolute;
            inset: 0;
            background: linear-gradient(90deg, rgba(255,255,255,0) 0%, rgba(255,255,255,0.8) 50%, rgba(255,255,255,0) 100%);
            animation: shimmer 1.8s infinite;
          }
          @keyframes fadeIn {
            from { opacity: 0; transform: translateY(10px); }
            to   { opacity: 1; transform: translateY(0); }
          }
          @keyframes pulse {
            0%, 100% { opacity: 0.6; }
            50%      { opacity: 0.8; }
          }
        `}
            </style>
        </div>
    );
};

export default InvestigationSummaryPanel;
