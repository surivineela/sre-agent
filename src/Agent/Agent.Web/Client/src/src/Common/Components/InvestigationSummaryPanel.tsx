import { Checkmark24Regular, ChevronDown24Regular, ChevronUp24Regular, DocumentSearch24Regular } from '@fluentui/react-icons';
import React, { useCallback, useEffect, useRef, useState } from 'react';
import ReactMarkdown from 'react-markdown';

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
    // ────────────────────────────────────────────────────────────────────────────
    // CUSTOM RENDERERS
    // ────────────────────────────────────────────────────────────────────────────
    const aLinkRenderer = useCallback((props: any) => {
        return (
            <a href={props.href} target="_blank" rel="noopener noreferrer">
                {props.children}
            </a>
        );
    }, []);

    // ────────────────────────────────────────────────────────────────────────────
    // STATE
    // ────────────────────────────────────────────────────────────────────────────
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    // Animation / layout flags
    const [mainExpanded, setMainExpanded] = useState(true); // Start expanded
    const [thinking, setThinking] = useState(true);
    const [visibleSections, setVisibleSections] = useState<number[]>([]);
    const [currentShimmerSection, setCurrentShimmerSection] = useState(0);
    const [complete, setComplete] = useState(false);

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

    const [containerTitle, setContainerTitle] = useState('Starting investigation and forming hypothesis');

    // ────────────────────────────────────────────────────────────────────────────
    // EFFECT: PARSE messageText
    // ────────────────────────────────────────────────────────────────────────────
    useEffect(() => {
        setIsLoading(true);
        setError(null);

        // Helpers -------------------------------------------------------------
        const safeJsonParse = <T,>(raw: string): T | null => {
            try {
                // Handle cases where the JSON might be a string within a string
                let jsonStr = raw.trim();

                // First try direct parsing
                try {
                    return JSON.parse(jsonStr) as T;
                } catch (e) {
                    // If that fails, try to see if it's a JSON string inside a string
                    // This handles cases where the JSON might be escaped or double-encoded
                    try {
                        // Look for objects that might be stringified JSON
                        if (jsonStr.startsWith('{') && jsonStr.endsWith('}')) {
                            // Already looks like JSON, but the previous parse failed
                            // Try to clean it up by handling escaped quotes
                            jsonStr = jsonStr.replace(/\\"/g, '"');
                            return JSON.parse(jsonStr) as T;
                        } else {
                            return null;
                        }
                    } catch {
                        return null;
                    }
                }
            } catch {
                return null;
            }
        };

        const parseLooseFormat = (text: string): void => {
            // Handle <final-summary>…</final-summary>
            const finalRegex = /<final-summary>([\s\S]*?)<\/final-summary>/i;
            const fMatch = text.match(finalRegex);
            if (fMatch?.[1]) {
                setFinalSummaryText(fMatch[1].trim());
                setFinalSummaryVisible(true);
            }

            // Try to extract multiple sections if they exist
            const sectionRegex = /<section title="([^"]+)">([\s\S]*?)<\/section>/gi;
            const sections: Section[] = [];
            let match;
            let id = 1;

            while ((match = sectionRegex.exec(text)) !== null) {
                sections.push({
                    id: id++,
                    title: match[1],
                    expanded: false,
                    thinking: false,
                    content: match[2].trim(),
                    status: 'completed',
                });
            }

            // If we found sections, use them
            if (sections.length > 0) {
                setSections(sections);
                setComplete(true);
                setThinking(false);
                setMainExpanded(false);
                return;
            }

            // Try to detect and parse JSON directly in the text
            const jsonRegex = /\{[\s\S]*"title"[\s\S]*"summary"[\s\S]*\}/i;
            const jsonMatch = text.match(jsonRegex);

            if (jsonMatch?.[0]) {
                try {
                    // Found what looks like a JSON object
                    const jsonData = JSON.parse(jsonMatch[0]);

                    if (jsonData.title && jsonData.summary) {
                        // This looks like a single summary item
                        setSections([
                            {
                                id: 1,
                                title: jsonData.title,
                                expanded: false,
                                thinking: false,
                                content: jsonData.summary,
                                status: 'completed',
                            },
                        ]);
                        setComplete(true);
                        setThinking(false);
                        setMainExpanded(false);
                        return;
                    }
                } catch (e) {
                    console.error('Failed to parse potential JSON in content', e);
                }
            }

            // Default fallback if no JSON detected
            const rawContent = text.replace(finalRegex, '').trim();
            setSections([
                {
                    id: 1,
                    title: 'Investigation Results',
                    expanded: false,
                    thinking: false,
                    content: rawContent,
                    status: 'completed',
                },
            ]);
            setComplete(true);
            setThinking(false);
            setMainExpanded(false);
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
                    if (finalItem) {
                        setFinalSummaryText(finalItem.summary);
                    }

                    const parsedSections: Section[] = data.summaries.map((s, idx) => ({
                        id: idx + 1,
                        title: s.title,
                        expanded: !(s.isCollapsed ?? true),
                        thinking: s.status === 'loading',
                        content: s.summary,
                        status: s.status ?? 'loading',
                    }));
                    const hasLoadingSections = parsedSections.some(s => s.status === 'loading');

                    setSections(parsedSections);

                    const allCompleted = parsedSections.every(s => s.status === 'completed');

                    // Update state based on section statuses
                    // Only set thinking mode if there are sections in a loading state
                    setThinking(hasLoadingSections);
                    setComplete(allCompleted);

                    // Make all sections visible immediately for completed sections
                    if (allCompleted) {
                        setVisibleSections(parsedSections.map(s => s.id));
                        setMainExpanded(false);

                        if (finalItem) {
                            setFinalSummaryVisible(true);
                        }
                    } else if (hasLoadingSections) {
                        // If still loading, expand the panel and prepare for animations
                        setMainExpanded(true);
                    } else {
                        // If no loading sections but not all complete, just make everything visible
                        setVisibleSections(parsedSections.map(s => s.id));
                    }
                } else {
                    // Invalid JSON – fall back to loose format so at least something shows up
                    console.error('Failed to parse investigation data, trying loose format');
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
    }, [messageText]);

    // ────────────────────────────────────────────────────────────────────────────
    // EFFECT: KICK-OFF animation only when there are loading sections
    // ────────────────────────────────────────────────────────────────────────────
    useEffect(() => {
        if (sections.length && thinking) {
            startAnimation();
        } else if (sections.length && !thinking) {
            // For non-animated state, just make all sections visible
            setVisibleSections(sections.map(s => s.id));
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [sections, thinking]);

    // ────────────────────────────────────────────────────────────────────────────
    // EFFECT: AUTO-COLLAPSE main container when everything is done
    // ────────────────────────────────────────────────────────────────────────────
    useEffect(() => {
        if (complete) {
            // When complete, collapse and show the final summary
            setMainExpanded(false);

            if (finalSummaryText) {
                setFinalSummaryVisible(true);
            }
        }
    }, [complete, finalSummaryText]);

    // ────────────────────────────────────────────────────────────────────────────
    // ANIMATION helpers
    // ────────────────────────────────────────────────────────────────────────────
    const startAnimation = () => {
        if (!thinking) return;

        setComplete(false);
        setVisibleSections([]);
        setCurrentShimmerSection(0);
        setTimeout(showNextSection, 500);
    };

    const showNextSection = () => {
        const allSections = sectionsRef.current;

        // Finished?
        if (visibleSections.length >= allSections.length) {
            setTimeout(() => {
                setCurrentShimmerSection(0);
                setThinking(false);
                setComplete(true);

                if (finalSummaryText) {
                    setFinalSummaryVisible(true);
                    setMainExpanded(false);
                }
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

        // Don't allow toggling while thinking/loading
        if (!thinking) {
            setMainExpanded(prev => !prev);
        }
    };

    const toggleSubSection = (id: number) => {
        userInteractedRef.current = true;

        // Always allow toggling subsections unless thinking
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
                        Error loading investigation
                    </p>
                    <p style={{ color: '#d32f2f', margin: 0, fontSize: 12 }}>{error}</p>
                </div>
            </div>
        );
    }

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
                                    <div className="thinking-indicator"></div>
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

                {/* ——————————————————— Sub-sections */}
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
                                width: '100%', // Ensure 100% width
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
                                                            style={{ color: '#333', fontSize: 13, display: 'flex', alignItems: 'center' }}
                                                        >
                                                            <span className="section-number">{section.id}.</span> {section.title}
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
                                                                    <div className="thinking-indicator"></div>
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
                                                        {/* For all section content, we use ReactMarkdown with specific styling */}
                                                        <div className="investigation-section-content">
                                                            <ReactMarkdown
                                                                components={{
                                                                    a: aLinkRenderer as React.ComponentType<any>,
                                                                }}
                                                            >
                                                                {section.content}
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
                            <ReactMarkdown components={{ a: aLinkRenderer }}>{finalSummaryText}</ReactMarkdown>
                        </div>
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
          
          @keyframes pulse {
            0%, 100% { opacity: 0.4; transform: scale(1); }
            50%      { opacity: 0.8; transform: scale(1.05); }
          }
          
          .thinking-indicator {
            position: absolute;
            right: 5px;
            top: 5px;
            width: 6px;
            height: 6px;
            border-radius: 50%;
            background-color: #2b88d8;
            animation: pulse 1.5s infinite;
          }
          
          @keyframes fadeIn {
            from { opacity: 0; transform: translateY(10px); }
            to   { opacity: 1; transform: translateY(0); }
          }
          
          @keyframes pulse-bg {
            0%, 100% { opacity: 0.6; }
            50%      { opacity: 0.8; }
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
          
          /* Add a subtle hover effect to indicate clickability */
          .complete-collapsed:hover {
            filter: brightness(1.05);
            transform: scale(1.01);
          }
          
          /* Make text in sections smaller and light grey */
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
        `}
            </style>
        </div>
    );
};

export default InvestigationSummaryPanel;
