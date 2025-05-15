import { Checkmark24Regular, ChevronDown24Regular, ChevronUp24Regular, DocumentSearch24Regular } from '@fluentui/react-icons';
import React, { useCallback, useEffect, useRef, useState } from 'react';
import ReactMarkdown from 'react-markdown';

// ────────────────────────────────────────────────────────────────────────────────
// TYPES
// ────────────────────────────────────────────────────────────────────────────────
export interface SummaryItem {
    title: string;
    summary: string;
    isCollapsed: boolean;
    status?: 'loading' | 'completed' | 'error';
    isFinal?: boolean;
}

export interface InvestigationSummaries {
    containerTitle: string;
    summaries: SummaryItem[];
}

export interface Section {
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

// ────────────────────────────────────────────────────────────────────────────────
// HELPER: merge new parse with current UI state (preserve expanded/collapsed)
// ────────────────────────────────────────────────────────────────────────────────
const mergeSectionState = (prev: Section[], next: Section[]): Section[] => {
    const prevById = new Map(prev.map(s => [s.id, s] as const));
    return next.map(s => {
        const old = prevById.get(s.id);
        return old ? { ...s, expanded: old.expanded } : s;
    });
};

// ────────────────────────────────────────────────────────────────────────────────
// COMPONENT
// ────────────────────────────────────────────────────────────────────────────────
const InvestigationSummaryPanel: React.FC<InvestigationSummaryPanelProps> = ({ messageText }) => {
    // ────────────────────────────────────────────────────────────────────────────
    // CUSTOM RENDERERS
    // ────────────────────────────────────────────────────────────────────────────
    const aLinkRenderer = useCallback(
        (props: any) => (
            <a href={props.href} target="_blank" rel="noopener noreferrer">
                {props.children}
            </a>
        ),
        []
    );

    // ────────────────────────────────────────────────────────────────────────────
    // STATE
    // ────────────────────────────────────────────────────────────────────────────
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    const [mainExpanded, setMainExpanded] = useState(true);
    const [thinking, setThinking] = useState(true);
    const [visibleSections, setVisibleSections] = useState<number[]>([]);
    const visibleSectionsRef = useRef<number[]>([]); //  ← NEW
    useEffect(() => {
        visibleSectionsRef.current = visibleSections;
    }, [visibleSections]);

    const [currentShimmerSection, setCurrentShimmerSection] = useState(0);
    const [complete, setComplete] = useState(false);

    const userInteractedRef = useRef(false);

    const [finalSummaryVisible, setFinalSummaryVisible] = useState(false);
    const [finalSummaryText, setFinalSummaryText] = useState('');

    const [sections, setSections] = useState<Section[]>([]);
    const sectionsRef = useRef<Section[]>([]);
    useEffect(() => {
        sectionsRef.current = sections;
    }, [sections]);

    const [containerTitle, setContainerTitle] = useState('Starting investigation and forming hypothesis');

    // ────────────────────────────────────────────────────────────────────────────
    // EFFECT: PARSE messageText
    // ────────────────────────────────────────────────────────────────────────────
    useEffect(() => {
        setIsLoading(true);
        setError(null);

        const safeJsonParse = <T,>(raw: string): T | null => {
            try {
                let jsonStr = raw.trim();
                try {
                    return JSON.parse(jsonStr) as T;
                } catch {
                    if (jsonStr.startsWith('{') && jsonStr.endsWith('}')) {
                        jsonStr = jsonStr.replace(/\\"/g, '"');
                        return JSON.parse(jsonStr) as T;
                    }
                    return null;
                }
            } catch {
                return null;
            }
        };

        const createPlaceholderSection = (): Section[] => [
            {
                id: 1,
                title: 'Working on investigation…',
                expanded: false,
                thinking: true,
                content: '',
                status: 'loading',
            },
        ];

        try {
            if (!messageText) {
                setIsLoading(false);
                return;
            }

            const summariesRegex = /<investigation-summaries>([\s\S]*?)<\/investigation-summaries>/i;
            const match = messageText.match(summariesRegex);

            if (!match?.[1]) {
                setSections(createPlaceholderSection());
                return;
            }

            const data = safeJsonParse<InvestigationSummaries>(match[1].trim());
            if (!data) {
                setSections(createPlaceholderSection());
                return;
            }

            if (data.containerTitle) setContainerTitle(data.containerTitle);

            const parsed = data.summaries.length
                ? data.summaries.map((s, idx) => ({
                      id: idx + 1,
                      title: s.title,
                      expanded: !(s.isCollapsed ?? true),
                      thinking: s.status === 'loading',
                      content: s.summary,
                      status: s.status ?? 'loading',
                  }))
                : createPlaceholderSection();

            const merged = mergeSectionState(sectionsRef.current, parsed);
            const prev = sectionsRef.current;

            // Was *any* new loading section introduced?
            const hasNewLoading = merged.some(s => s.status === 'loading' && !prev.find(p => p.id === s.id));

            // Preserve already-visible ids; don't nuke the list on every update
            setVisibleSections(old => merged.filter(s => old.includes(s.id) || s.status !== 'loading').map(s => s.id));

            setSections(merged);

            const stillThinking = merged.some(s => s.status === 'loading');
            setThinking(stillThinking);
            const allDone = merged.every(s => s.status === 'completed');
            setComplete(allDone);

            const finalItem = data.summaries.find(s => s.isFinal);
            if (finalItem) {
                setFinalSummaryText(finalItem.summary);
                if (allDone) setFinalSummaryVisible(true);
            }

            if (hasNewLoading) {
                // Only restart the step-reveal animation if we truly got new work
                setCurrentShimmerSection(0);
                setTimeout(showNextSection, 500);
            }
        } catch (err) {
            console.error('Error processing message text', err);
            setError('Failed to process investigation message');
            setSections(createPlaceholderSection());
        } finally {
            setIsLoading(false);
        }
    }, [messageText]);

    // ────────────────────────────────────────────────────────────────────────────
    // EFFECT: AUTO-COLLAPSE main container when everything is done
    // ────────────────────────────────────────────────────────────────────────────
    useEffect(() => {
        if (complete && finalSummaryText && !userInteractedRef.current) {
            setMainExpanded(false);
            setFinalSummaryVisible(true);
        }
    }, [complete, finalSummaryText]);

    // ────────────────────────────────────────────────────────────────────────────
    // ANIMATION helpers
    // ────────────────────────────────────────────────────────────────────────────
    const showNextSection = () => {
        // NB:  *always* read from refs inside a timer-callback
        const allSections = sectionsRef.current;
        const seenIds = visibleSectionsRef.current;

        if (seenIds.length >= allSections.length) return; // nothing left to reveal

        const nextId = allSections[seenIds.length].id;

        // Auto-expand the incoming section
        setSections(prev => prev.map(s => (s.id === nextId ? { ...s, expanded: true } : s)));

        setCurrentShimmerSection(nextId);
        setVisibleSections(prev => {
            const updated = [...prev, nextId];
            visibleSectionsRef.current = updated;
            return updated;
        });

        // Schedule the next one, unless every section is already visible
        if (seenIds.length + 1 < allSections.length) {
            setTimeout(showNextSection, 1200);
        } else {
            // Last one – once its status flips to "completed", thinking → false
            const checkDone = () => {
                const nowDone = sectionsRef.current.every(s => s.status === 'completed');
                if (nowDone) {
                    setThinking(false);
                    setComplete(true);
                    if (finalSummaryText) setFinalSummaryVisible(true);
                    if (!userInteractedRef.current) setMainExpanded(false);
                } else {
                    // keep polling every half-second until done
                    setTimeout(checkDone, 500);
                }
            };
            setTimeout(checkDone, 800);
        }
    };

    // ────────────────────────────────────────────────────────────────────────────
    // CLICK handlers
    // ────────────────────────────────────────────────────────────────────────────
    const toggleMainSection = () => {
        userInteractedRef.current = true;
        setMainExpanded(prev => !prev);
    };

    const toggleSubSection = (id: number) => {
        userInteractedRef.current = true;
        setSections(prev => prev.map(s => (s.id === id ? { ...s, expanded: !s.expanded } : s)));
    };

    // ────────────────────────────────────────────────────────────────────────────
    // RENDER helpers – skeleton & error short‑circuits
    // ────────────────────────────────────────────────────────────────────────────
    if (isLoading)
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

    if (error)
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
                        Error loading investigation
                    </p>
                    <p style={{ color: '#d32f2f', margin: 0, fontSize: 12 }}>{error}</p>
                </div>
            </div>
        );

    // ────────────────────────────────────────────────────────────────────────────
    // MAIN RENDER
    // ────────────────────────────────────────────────────────────────────────────
    return (
        <div style={{ width: '100%', marginBottom: 16, marginTop: 16 }}>
            {/* ——————————————————— Main header */}
            <div
                style={{
                    width: '100%',
                    transition: 'all 400ms ease',
                    transform: !mainExpanded && complete ? 'scale(0.95)' : 'scale(1)',
                }}
            >
                <div
                    style={{
                        backgroundColor: '#f9f9f9',
                        padding: !mainExpanded && complete ? 7 : 12,
                        borderRadius: 8,
                        border: '1px solid #e0e0e0',
                        marginBottom: 4,
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'space-between',
                        cursor: 'pointer',
                        transition: 'all 300ms',
                        opacity: !mainExpanded && complete ? 0.85 : 1,
                        boxShadow: '0 1px 3px rgba(0,0,0,0.08)',
                        width: '100%',
                    }}
                    onClick={toggleMainSection}
                    className={!mainExpanded && complete ? 'complete-collapsed' : ''}
                >
                    <div style={{ display: 'flex', alignItems: 'center' }}>
                        <DocumentSearch24Regular
                            style={{
                                color: '#888',
                                marginRight: 12,
                                width: !mainExpanded && complete ? 16 : 20,
                                height: !mainExpanded && complete ? 16 : 20,
                                transition: 'all 300ms',
                            }}
                        />
                        <div style={{ position: 'relative' }}>
                            <span
                                style={{
                                    fontWeight: 500,
                                    color: !mainExpanded && complete ? '#777' : '#333',
                                    fontSize: !mainExpanded && complete ? 11 : 14,
                                    transition: 'all 300ms',
                                }}
                            >
                                {containerTitle}
                            </span>
                            {!finalSummaryVisible && thinking && (
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
                                color: complete ? '#666' : '#ccc',
                                marginRight: 8,
                                width: !mainExpanded && complete ? 16 : 20,
                                height: !mainExpanded && complete ? 16 : 20,
                                transition: 'all 300ms',
                            }}
                        />
                        {mainExpanded ? (
                            <ChevronUp24Regular
                                style={{
                                    color: '#666',
                                    width: !mainExpanded && complete ? 16 : 20,
                                    height: !mainExpanded && complete ? 16 : 20,
                                    transition: 'all 300ms',
                                }}
                            />
                        ) : (
                            <ChevronDown24Regular
                                style={{
                                    color: '#666',
                                    width: !mainExpanded && complete ? 16 : 20,
                                    height: !mainExpanded && complete ? 16 : 20,
                                    transition: 'all 300ms',
                                }}
                            />
                        )}
                    </div>
                </div>

                {/* ——————————————————— Sub‑sections */}
                <div
                    style={{
                        overflow: 'visible',
                        transition: 'all 600ms ease',
                        maxHeight: mainExpanded ? 'none' : 0,
                        opacity: mainExpanded ? 1 : 0,
                        transform: mainExpanded ? 'translateY(0)' : 'translateY(-8px)',
                        width: '100%',
                        boxSizing: 'border-box',
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
                                backgroundImage: 'linear-gradient(to bottom, #ffffff, #fafafa)',
                                boxShadow: 'inset 0 1px 3px rgba(0,0,0,0.03)',
                                width: '100%',
                            }}
                        >
                            {sections.map(section => {
                                // PLACEHOLDER while animating
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

                                // VISIBLE section
                                if (visibleSections.includes(section.id)) {
                                    return (
                                        <div
                                            key={section.id}
                                            style={{
                                                marginBottom: 12,
                                                animation: thinking ? 'fadeIn 0.45s ease' : 'none',
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
                                                    <DocumentSearch24Regular style={{ color: '#888', marginRight: 8 }} />
                                                    <div style={{ position: 'relative' }}>
                                                        <span
                                                            style={{
                                                                color: '#333',
                                                                fontSize: 13,
                                                                display: 'flex',
                                                                alignItems: 'center',
                                                            }}
                                                        >
                                                            <span className="section-number">{section.id}. </span>
                                                            {section.title}
                                                        </span>
                                                        {thinking &&
                                                            currentShimmerSection === section.id &&
                                                            section.status === 'loading' && (
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
                                                            margin: '8px 0',
                                                            width: '100%',
                                                            boxSizing: 'border-box',
                                                            overflowX: 'auto',
                                                            boxShadow: 'inset 0 1px 3px rgba(0,0,0,0.05)',
                                                        }}
                                                    >
                                                        <div className="investigation-section-content">
                                                            <ReactMarkdown components={{ a: aLinkRenderer as any }}>
                                                                {section.content || '*Working…*'}
                                                            </ReactMarkdown>
                                                        </div>
                                                    </div>
                                                )}
                                            </div>
                                        </div>
                                    );
                                }
                                return null;
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
                        animation: complete && thinking ? 'fadeIn 0.4s ease' : 'none',
                        transform: !mainExpanded && complete ? 'scale(0.96)' : 'scale(1)',
                        transition: 'all 600ms ease',
                        boxShadow: '0 2px 4px rgba(0,0,0,0.06)',
                        background: 'linear-gradient(to bottom, #fcfcfc, #f9f9f9)',
                    }}
                >
                    <div style={{ display: 'flex', flexDirection: 'column' }}>
                        <span
                            style={{
                                fontWeight: 500,
                                color: '#333',
                                marginBottom: 8,
                                fontSize: 14,
                                borderBottom: '1px solid #eaeaea',
                                paddingBottom: 6,
                            }}
                        >
                            Final Summary:
                        </span>
                        <div style={{ color: '#555' }}>
                            <ReactMarkdown components={{ a: aLinkRenderer as any }}>{finalSummaryText}</ReactMarkdown>
                        </div>
                    </div>
                </div>
            )}

            {/* ——————————————————— Inline CSS for shimmer / animations */}
            <style>{`
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

        @keyframes pulse {
          0%, 100% { opacity: 0.4; transform: scale(1); }
          50%      { opacity: 0.8; transform: scale(1.05); }
        }

        @keyframes fadeIn {
          from { opacity: 0; transform: translateY(10px); }
          to   { opacity: 1; transform: translateY(0); }
        }

        .section-number {
          display: inline-block;
          min-width: 18px;
          height: 18px;
          line-height: 18px;
          text-align: center;
          background-color: rgba(0,0,0,0.05);
          border-radius: 9px;
          font-size: 10px;
          margin-right: 8px;
          color: #777;
        }

        .complete-collapsed:hover {
          filter: brightness(1.05);
          transform: scale(1.01);
        }

        .investigation-section-content {
          font-size: 11px;
          color: #666;
          line-height: 1.4;
        }
        .investigation-section-content p {
          margin-top: 0.5em;
          margin-bottom: 0.5em;
        }
        .investigation-section-content h2,
        .investigation-section-content h3 {
          color: #555;
          font-size: 13px;
          margin-top: 0.75em;
          margin-bottom: 0.5em;
          font-weight: 500;
          padding-bottom: 3px;
          border-bottom: 1px solid rgba(0,0,0,0.05);
        }
        .investigation-section-content strong {
          color: #444;
          font-weight: 600;
        }
        .investigation-section-content h2 {
          font-size: 13.5px;
        }
        .investigation-section-content code {
          background: #f1f1f1;
          padding: 1px 4px;
          border-radius: 3px;
          font-size: 11px;
        }
        .investigation-section-content ul,
        .investigation-section-content ol {
          padding-left: 1.5em;
          margin: 0.5em 0;
        }
      `}</style>
        </div>
    );
};

export default InvestigationSummaryPanel;
