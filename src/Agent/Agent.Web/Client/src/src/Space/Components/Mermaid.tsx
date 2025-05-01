import mermaid from 'mermaid';
import React, { useEffect, useRef, useState } from 'react';

interface MermaidChartProps {
    chart: string;
    title?: string;
}

// Initialize mermaid with styling consistent with the charts
mermaid.initialize({
    startOnLoad: false,
    theme: 'neutral',
    flowchart: {
        useMaxWidth: false,
        htmlLabels: true,
        curve: 'basis',
    },
    themeVariables: {
        primaryColor: '#4F46E5', // Using the first color from CHART_COLORS
        primaryTextColor: '#FFFFFF',
        primaryBorderColor: '#3730A3',
        lineColor: '#6B7280',
        secondaryColor: '#10B981', // Using the second color from CHART_COLORS
        tertiaryColor: '#F59E0B', // Using the third color from CHART_COLORS
    },
    fontFamily: '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif',
    fontSize: 16,
});

const MermaidChart: React.FC<MermaidChartProps> = ({ chart, title }) => {
    const chartRef = useRef<HTMLDivElement>(null);
    const modalChartRef = useRef<HTMLDivElement>(null);
    const [error, setError] = useState<string | null>(null);
    const [svgContent, setSvgContent] = useState<string>('');
    const [isModalOpen, setIsModalOpen] = useState<boolean>(false);
    const [scale, setScale] = useState<number>(1);
    const [position, setPosition] = useState({ x: 0, y: 0 });
    const [isDragging, setIsDragging] = useState(false);
    const [startPosition, setStartPosition] = useState({ x: 0, y: 0 });

    // Function to download diagram as SVG file
    const downloadSVG = () => {
        if (!svgContent) return;

        const blob = new Blob([svgContent], { type: 'image/svg+xml' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `${title || 'mermaid-diagram'}.svg`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    };

    // Function to download diagram as PNG
    const downloadPNG = () => {
        const svgElement = (isModalOpen ? modalChartRef.current : chartRef.current)?.querySelector('svg');
        if (!svgElement) return;

        // Create a copy of the SVG element to manipulate for export
        const clonedSvg = svgElement.cloneNode(true) as SVGElement;

        // Reset transform properties for export
        clonedSvg.style.transform = '';
        clonedSvg.style.transition = '';

        const svgSize = svgElement.getBoundingClientRect();

        // Set canvas dimensions with a scale factor for better quality
        const scaleFactor = 2; // Higher resolution
        const canvas = document.createElement('canvas');
        canvas.width = svgSize.width * scaleFactor;
        canvas.height = svgSize.height * scaleFactor;

        const ctx = canvas.getContext('2d');
        if (!ctx) {
            console.error('Failed to get canvas context');
            return;
        }

        // Scale for higher resolution
        ctx.scale(scaleFactor, scaleFactor);

        // Create image from SVG
        const img = new Image();

        // Convert SVG to data URL
        const svgData = new XMLSerializer().serializeToString(clonedSvg);
        const svgBlob = new Blob([svgData], { type: 'image/svg+xml;charset=utf-8' });
        const svgUrl = URL.createObjectURL(svgBlob);

        img.onload = () => {
            ctx.fillStyle = '#FFFFFF';
            ctx.fillRect(0, 0, canvas.width / scaleFactor, canvas.height / scaleFactor);

            ctx.drawImage(img, 0, 0);

            const pngUrl = canvas.toDataURL('image/png');
            const a = document.createElement('a');
            a.href = pngUrl;
            a.download = `${title || 'mermaid-diagram'}.png`;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);

            // Clean up
            URL.revokeObjectURL(svgUrl);
        };

        img.src = svgUrl;
    };

    // Toggle fullscreen modal
    const toggleModal = () => {
        setIsModalOpen(!isModalOpen);
        // Reset zoom and position when opening/closing modal
        setScale(1);
        setPosition({ x: 0, y: 0 });
    };

    // Handle zoom in/out
    const handleZoom = (zoomIn: boolean) => {
        const newScale = zoomIn
            ? Math.min(scale + 0.2, 3) // Max zoom 3x
            : Math.max(scale - 0.2, 0.5); // Min zoom 0.5x
        setScale(newScale);
    };

    // Reset zoom and position
    const resetView = () => {
        setScale(1);
        setPosition({ x: 0, y: 0 });
    };

    // Handle drag start
    const handleMouseDown = (e: React.MouseEvent) => {
        setIsDragging(true);
        setStartPosition({
            x: e.clientX - position.x,
            y: e.clientY - position.y,
        });
    };

    // Handle drag move
    const handleMouseMove = (e: React.MouseEvent) => {
        if (!isDragging) return;

        const newX = e.clientX - startPosition.x;
        const newY = e.clientY - startPosition.y;

        setPosition({ x: newX, y: newY });
    };

    // Handle drag end
    const handleMouseUp = () => {
        setIsDragging(false);
    };

    // Handle mouse leave
    const handleMouseLeave = () => {
        if (isDragging) {
            setIsDragging(false);
        }
    };

    // Handle keyboard events in modal
    const handleKeyDown = (e: KeyboardEvent) => {
        if (!isModalOpen) return;

        switch (e.key) {
            case 'Escape':
                toggleModal();
                break;
            case '+':
            case '=':
                handleZoom(true);
                break;
            case '-':
                handleZoom(false);
                break;
            case '0':
                resetView();
                break;
        }
    };

    // Add keyboard event listener for modal
    useEffect(() => {
        window.addEventListener('keydown', handleKeyDown);
        return () => {
            window.removeEventListener('keydown', handleKeyDown);
        };
    }, [isModalOpen, scale]);

    // Prevent body scrolling when modal is open
    useEffect(() => {
        if (isModalOpen) {
            document.body.style.overflow = 'hidden';
        } else {
            document.body.style.overflow = '';
        }
        return () => {
            document.body.style.overflow = '';
        };
    }, [isModalOpen]);

    // Render mermaid diagram
    const renderMermaidDiagram = (containerRef: React.RefObject<HTMLDivElement>) => {
        if (containerRef.current && chart) {
            const id = `mermaid-${Math.random().toString(36).substring(2, 11)}`;

            try {
                mermaid
                    .render(id, chart)
                    .then(({ svg }) => {
                        if (containerRef.current) {
                            // Store SVG content for downloading
                            setSvgContent(svg);

                            // Add unique class to the diagram
                            const enhancedSvg = svg.replace('<svg ', `<svg class="mermaid-svg-${id}" `);

                            // Render the SVG
                            containerRef.current.innerHTML = enhancedSvg;

                            // Apply custom styling to the SVG after rendering
                            const svgElement = containerRef.current.querySelector('svg');
                            if (svgElement instanceof SVGElement) {
                                // Apply responsive styling
                                svgElement.style.maxWidth = '100%';
                                svgElement.style.height = 'auto';

                                // Apply font styling to all text elements
                                const textElements = svgElement.querySelectorAll('text');
                                textElements.forEach(text => {
                                    if (text instanceof SVGTextElement) {
                                        text.style.fontFamily =
                                            '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif';
                                        text.style.fontSize = '13px';
                                        text.style.fontWeight = '500';
                                    }
                                });

                                // Style the nodes to match chart styling
                                const nodeElements = svgElement.querySelectorAll(
                                    '.node rect, .node circle, .node ellipse, .node polygon, .node path'
                                );
                                nodeElements.forEach(node => {
                                    if (node instanceof SVGElement) {
                                        node.style.stroke = '#4F46E5'; // Primary color from chart
                                        node.style.strokeWidth = '1.5px';
                                        node.style.filter = 'drop-shadow(0px 1px 2px rgba(0, 0, 0, 0.1))';
                                    }
                                });

                                // Style the edges to match chart styling
                                const edgeElements = svgElement.querySelectorAll('.edge path');
                                edgeElements.forEach(edge => {
                                    if (edge instanceof SVGElement) {
                                        edge.style.stroke = '#6B7280'; // Softer gray that matches chart grid
                                        edge.style.strokeWidth = '1.5px';
                                        edge.style.opacity = '0.8';
                                    }
                                });

                                // Style edge labels with better readability
                                const edgeLabels = svgElement.querySelectorAll('.edgeLabel');
                                edgeLabels.forEach(label => {
                                    if (label instanceof SVGElement) {
                                        label.style.background = '#FFFFFF';
                                        label.style.borderRadius = '4px';
                                        label.style.padding = '2px 4px';
                                        label.style.boxShadow = '0 1px 2px rgba(0, 0, 0, 0.05)';
                                    }
                                });

                                // Style arrowheads to match line colors
                                const markers = svgElement.querySelectorAll('marker path');
                                markers.forEach(marker => {
                                    if (marker instanceof SVGElement) {
                                        marker.style.fill = '#6B7280'; // Match line color
                                        marker.style.stroke = '#6B7280'; // Match line color
                                    }
                                });

                                // Apply zoom effect and position based on scale state
                                svgElement.style.transform = `scale(${scale}) translate(${position.x / scale}px, ${position.y / scale}px)`;
                                svgElement.style.transformOrigin = 'center';
                                svgElement.style.transition = isDragging ? 'none' : 'transform 0.2s ease';
                                svgElement.style.cursor = isDragging ? 'grabbing' : 'grab';
                            }
                        }
                    })
                    .catch(err => {
                        console.error('Error rendering mermaid chart:', err);
                        setError(err.message);
                    });
            } catch (err) {
                if (err instanceof Error) {
                    console.error('Error rendering mermaid chart:', err);
                    setError(err.message);
                } else {
                    console.error('Unknown error rendering mermaid chart');
                    setError('Unknown error');
                }
            }
        }
    };

    // Render mermaid in container
    useEffect(() => {
        renderMermaidDiagram(chartRef);
    }, [chart]);

    // Render mermaid in modal when modal is opened
    useEffect(() => {
        if (isModalOpen && modalChartRef.current) {
            renderMermaidDiagram(modalChartRef);
        }
    }, [isModalOpen, chart]);

    // Update SVG transform when scale or position changes
    useEffect(() => {
        const ref = isModalOpen ? modalChartRef : chartRef;
        if (ref.current) {
            const svgElement = ref.current.querySelector('svg');
            if (svgElement instanceof SVGElement) {
                svgElement.style.transform = `scale(${scale}) translate(${position.x / scale}px, ${position.y / scale}px)`;
                svgElement.style.transformOrigin = 'center';
                svgElement.style.transition = isDragging ? 'none' : 'transform 0.2s ease';
                svgElement.style.cursor = isDragging ? 'grabbing' : 'grab';
            }
        }
    }, [scale, position, isDragging, isModalOpen]);

    if (error) {
        return (
            <div
                className="mermaid-error"
                style={{
                    backgroundColor: '#FEE2E2',
                    color: '#B91C1C',
                    padding: '1rem',
                    borderRadius: '0.5rem',
                    fontFamily: '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif',
                    fontSize: '0.875rem',
                    marginBottom: '1rem',
                    boxShadow: '0 1px 3px rgba(0, 0, 0, 0.1)',
                }}
            >
                Error rendering chart: {error}
            </div>
        );
    }

    return (
        <>
            <div
                className="mermaid-container"
                style={{
                    backgroundColor: '#FAFBFC',
                    borderRadius: '0.75rem',
                    padding: '1.5rem',
                    boxShadow: '0 1px 3px rgba(0, 0, 0, 0.05)',
                    marginBottom: '1.5rem',
                    border: '1px solid #f0f2f5',
                    overflow: 'hidden',
                    position: 'relative',
                }}
                onClick={toggleModal}
            >
                {title && (
                    <div
                        style={{
                            fontSize: '1.25rem',
                            fontWeight: '700',
                            color: '#111827',
                            marginBottom: '1.25rem',
                            fontFamily: '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif',
                        }}
                    >
                        {title}
                    </div>
                )}

                <div
                    className="mermaid-controls"
                    style={{
                        position: 'absolute',
                        top: '1rem',
                        right: '1rem',
                        display: 'flex',
                        gap: '0.5rem',
                        zIndex: 10,
                    }}
                >
                    <button
                        onClick={e => {
                            e.stopPropagation();
                            toggleModal();
                        }}
                        style={{
                            backgroundColor: '#F9FAFB',
                            border: '1px solid #E5E7EB',
                            borderRadius: '0.375rem',
                            padding: '0.375rem 0.5rem',
                            cursor: 'pointer',
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'center',
                            boxShadow: '0 1px 2px rgba(0, 0, 0, 0.05)',
                            transition: 'all 0.2s ease',
                        }}
                        aria-label="Fullscreen"
                        title="Fullscreen"
                    >
                        <svg width="16" height="16" viewBox="0 0 20 20" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path
                                d="M3 8V5C3 3.89543 3.89543 3 5 3H8M17 8V5C17 3.89543 16.1046 3 15 3H12M3 12V15C3 16.1046 3.89543 17 5 17H8M17 12V15C17 16.1046 16.1046 17 15 17H12"
                                stroke="#666666"
                                strokeWidth="2"
                                strokeLinecap="round"
                                strokeLinejoin="round"
                            />
                        </svg>
                    </button>
                </div>

                <div
                    className="chart-container"
                    style={{
                        position: 'relative',
                        overflow: 'hidden',
                        borderRadius: '0.5rem',
                        cursor: 'pointer',
                        minHeight: '150px',
                    }}
                >
                    <div
                        ref={chartRef}
                        className="mermaid-chart"
                        style={{
                            overflowX: 'visible',
                            overflowY: 'visible',
                            padding: '1rem',
                            margin: '0 auto',
                            textAlign: 'center',
                            position: 'relative',
                        }}
                    ></div>

                    <div
                        style={{
                            position: 'absolute',
                            bottom: '1rem',
                            left: '50%',
                            transform: 'translateX(-50%)',
                            backgroundColor: 'rgba(255, 255, 255, 0.8)',
                            padding: '0.5rem 0.75rem',
                            borderRadius: '0.5rem',
                            fontSize: '0.75rem',
                            color: '#555555',
                            boxShadow: '0 1px 2px rgba(0, 0, 0, 0.1)',
                            pointerEvents: 'none',
                        }}
                    >
                        Click to open fullscreen
                    </div>
                </div>
            </div>

            {isModalOpen && (
                <div
                    className="mermaid-modal-overlay"
                    style={{
                        position: 'fixed',
                        top: 0,
                        left: 0,
                        right: 0,
                        bottom: 0,
                        backgroundColor: 'rgba(0, 0, 0, 0.5)',
                        zIndex: 1000,
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        padding: '2rem',
                    }}
                    onClick={toggleModal}
                >
                    <div
                        className="mermaid-modal-content"
                        style={{
                            backgroundColor: 'white',
                            borderRadius: '0.75rem',
                            padding: '2rem',
                            boxShadow: '0 10px 25px -5px rgba(0, 0, 0, 0.1), 0 10px 10px -5px rgba(0, 0, 0, 0.04)',
                            maxWidth: '95%',
                            maxHeight: '95%',
                            width: '90%',
                            height: '90%',
                            overflow: 'hidden',
                            display: 'flex',
                            flexDirection: 'column',
                            position: 'relative',
                        }}
                        onClick={e => e.stopPropagation()}
                    >
                        <div
                            style={{
                                display: 'flex',
                                justifyContent: 'space-between',
                                alignItems: 'center',
                                marginBottom: '1rem',
                            }}
                        >
                            {title && (
                                <h2
                                    style={{
                                        fontSize: '1.25rem',
                                        fontWeight: '700',
                                        color: '#111827',
                                        margin: 0,
                                        fontFamily: '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif',
                                    }}
                                >
                                    {title}
                                </h2>
                            )}

                            <div style={{ display: 'flex', gap: '0.5rem' }}>
                                <button
                                    onClick={resetView}
                                    style={{
                                        backgroundColor: '#F9FAFB',
                                        border: '1px solid #E5E7EB',
                                        borderRadius: '0.375rem',
                                        padding: '0.375rem 0.5rem',
                                        cursor: 'pointer',
                                        display: 'flex',
                                        alignItems: 'center',
                                        justifyContent: 'center',
                                        boxShadow: '0 1px 2px rgba(0, 0, 0, 0.05)',
                                        transition: 'all 0.2s ease',
                                    }}
                                    aria-label="Reset View"
                                    title="Reset View"
                                >
                                    <svg width="16" height="16" viewBox="0 0 20 20" fill="none" xmlns="http://www.w3.org/2000/svg">
                                        <path
                                            d="M4 4V9H9M16 16V11H11M16 4L12 8M4 16L8 12"
                                            stroke="#666666"
                                            strokeWidth="2"
                                            strokeLinecap="round"
                                            strokeLinejoin="round"
                                        />
                                    </svg>
                                </button>
                                <button
                                    onClick={() => handleZoom(false)}
                                    style={{
                                        backgroundColor: '#F9FAFB',
                                        border: '1px solid #E5E7EB',
                                        borderRadius: '0.375rem',
                                        padding: '0.375rem 0.5rem',
                                        cursor: 'pointer',
                                        display: 'flex',
                                        alignItems: 'center',
                                        justifyContent: 'center',
                                        boxShadow: '0 1px 2px rgba(0, 0, 0, 0.05)',
                                        transition: 'all 0.2s ease',
                                    }}
                                    aria-label="Zoom Out"
                                    title="Zoom Out"
                                >
                                    <svg width="16" height="16" viewBox="0 0 20 20" fill="none" xmlns="http://www.w3.org/2000/svg">
                                        <path d="M5 10H15" stroke="#666666" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                                    </svg>
                                </button>
                                <button
                                    onClick={() => handleZoom(true)}
                                    style={{
                                        backgroundColor: '#F9FAFB',
                                        border: '1px solid #E5E7EB',
                                        borderRadius: '0.375rem',
                                        padding: '0.375rem 0.5rem',
                                        cursor: 'pointer',
                                        display: 'flex',
                                        alignItems: 'center',
                                        justifyContent: 'center',
                                        boxShadow: '0 1px 2px rgba(0, 0, 0, 0.05)',
                                        transition: 'all 0.2s ease',
                                    }}
                                    aria-label="Zoom In"
                                    title="Zoom In"
                                >
                                    <svg width="16" height="16" viewBox="0 0 20 20" fill="none" xmlns="http://www.w3.org/2000/svg">
                                        <path
                                            d="M10 5V10M10 10V15M10 10H5M10 10H15"
                                            stroke="#666666"
                                            strokeWidth="2"
                                            strokeLinecap="round"
                                            strokeLinejoin="round"
                                        />
                                    </svg>
                                </button>
                                <button
                                    onClick={downloadSVG}
                                    style={{
                                        backgroundColor: '#F9FAFB',
                                        border: '1px solid #E5E7EB',
                                        borderRadius: '0.375rem',
                                        padding: '0.375rem 0.5rem',
                                        cursor: 'pointer',
                                        display: 'flex',
                                        alignItems: 'center',
                                        justifyContent: 'center',
                                        boxShadow: '0 1px 2px rgba(0, 0, 0, 0.05)',
                                        transition: 'all 0.2s ease',
                                    }}
                                    aria-label="Download SVG"
                                    title="Download SVG"
                                >
                                    <svg width="16" height="16" viewBox="0 0 20 20" fill="none" xmlns="http://www.w3.org/2000/svg">
                                        <path
                                            d="M7 10L10 13M10 13L13 10M10 13V4M19 14V16C19 17.1046 18.1046 18 17 18H3C1.89543 18 1 17.1046 1 16V14"
                                            stroke="#666666"
                                            strokeWidth="2"
                                            strokeLinecap="round"
                                            strokeLinejoin="round"
                                        />
                                    </svg>
                                </button>
                                <button
                                    onClick={downloadPNG}
                                    style={{
                                        backgroundColor: '#F9FAFB',
                                        border: '1px solid #E5E7EB',
                                        borderRadius: '0.375rem',
                                        padding: '0.375rem 0.5rem',
                                        cursor: 'pointer',
                                        display: 'flex',
                                        alignItems: 'center',
                                        justifyContent: 'center',
                                        boxShadow: '0 1px 2px rgba(0, 0, 0, 0.05)',
                                        transition: 'all 0.2s ease',
                                    }}
                                    aria-label="Download PNG"
                                    title="Download PNG"
                                >
                                    <svg width="16" height="16" viewBox="0 0 20 20" fill="none" xmlns="http://www.w3.org/2000/svg">
                                        <path
                                            d="M4 16L4 17M8 16L8 17M12 16L12 17M16 16L16 17M5 4H15M5 4C3.89543 4 3 4.89543 3 6V13C3 14.1046 3.89543 15 5 15H15C16.1046 15 17 14.1046 17 13V6C17 4.89543 16.1046 4 15 4"
                                            stroke="#666666"
                                            strokeWidth="2"
                                            strokeLinecap="round"
                                            strokeLinejoin="round"
                                        />
                                    </svg>
                                </button>
                                <button
                                    onClick={toggleModal}
                                    style={{
                                        backgroundColor: '#F9FAFB',
                                        border: '1px solid #E5E7EB',
                                        borderRadius: '0.375rem',
                                        padding: '0.375rem 0.5rem',
                                        cursor: 'pointer',
                                        display: 'flex',
                                        alignItems: 'center',
                                        justifyContent: 'center',
                                        boxShadow: '0 1px 2px rgba(0, 0, 0, 0.05)',
                                        transition: 'all 0.2s ease',
                                    }}
                                    aria-label="Close"
                                    title="Close"
                                >
                                    <svg width="16" height="16" viewBox="0 0 20 20" fill="none" xmlns="http://www.w3.org/2000/svg">
                                        <path
                                            d="M6 6L14 14M6 14L14 6"
                                            stroke="#666666"
                                            strokeWidth="2"
                                            strokeLinecap="round"
                                            strokeLinejoin="round"
                                        />
                                    </svg>
                                </button>
                            </div>
                        </div>

                        <div
                            style={{
                                flex: 1,
                                backgroundColor: '#FAFBFC',
                                borderRadius: '0.5rem',
                                overflow: 'hidden',
                                position: 'relative',
                            }}
                        >
                            <div
                                ref={modalChartRef}
                                className="mermaid-modal-chart"
                                style={{
                                    width: '100%',
                                    height: '100%',
                                    display: 'flex',
                                    alignItems: 'center',
                                    justifyContent: 'center',
                                    padding: '2rem',
                                    overflow: 'hidden',
                                    position: 'relative',
                                }}
                                onMouseDown={handleMouseDown}
                                onMouseMove={handleMouseMove}
                                onMouseUp={handleMouseUp}
                                onMouseLeave={handleMouseLeave}
                            ></div>
                        </div>

                        <div
                            style={{
                                padding: '0.75rem',
                                background: 'rgba(249, 250, 251, 0.8)',
                                borderRadius: '0.5rem',
                                margin: '1rem 0 0',
                                fontSize: '0.875rem',
                                color: '#666666',
                                fontFamily: '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif',
                                textAlign: 'center',
                            }}
                        >
                            <span style={{ fontWeight: 'bold' }}>Tip:</span> Click and drag to move • Scroll or use buttons to zoom • Press
                            ESC to close
                        </div>
                    </div>
                </div>
            )}
        </>
    );
};

export default MermaidChart;
