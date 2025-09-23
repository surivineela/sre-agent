import {
    CalendarRegular,
    ChevronDownRegular,
    ChevronUpRegular,
    CopyRegular,
    DocumentTextRegular,
    FullScreenMaximizeRegular,
    FullScreenMinimizeRegular,
    HistoryRegular,
    PersonRegular,
} from '@fluentui/react-icons';
import { useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { ChangeDiffResources, SreAgentResources } from '../../Strings/SREAgentResources';

interface ChangeDiffItem {
    changeTime: string;
    targetResourceId: string;
    changeType: string;
    changedBy: string;
    clientType: string;
    changes?: unknown;
    changesJson?: string;
    previousSnapshotId?: string;
    newSnapshotId?: string;
}

interface PropertyChange {
    previousValue: any;
    newValue: any;
}
interface ParsedChanges {
    [propertyPath: string]: PropertyChange;
}

const parseChanges = (c: ChangeDiffItem): ParsedChanges => {
    if (c.changes !== undefined && typeof c.changes !== 'string') {
        // No empty catch: just return when the shape is already object-like
        if (c.changes && typeof c.changes === 'object') {
            return c.changes as ParsedChanges;
        }
    }
    if (typeof c.changesJson === 'string') {
        try {
            return JSON.parse(c.changesJson) as ParsedChanges;
        } catch {
            return {};
        }
    }
    return {};
};

// ---------- small modal (click outside & ESC to close, locks scroll)
const Modal = ({ onClose, children, title }: { onClose: () => void; children: React.ReactNode; title?: string }) => {
    const intl = useIntl();
    const effectiveTitle = title || intl.formatMessage(ChangeDiffResources.changeDiffTitle);
    useEffect(() => {
        const onKey = (e: KeyboardEvent) => {
            if (e.key === 'Escape') onClose();
        };
        document.addEventListener('keydown', onKey);
        const prev = document.body.style.overflow;
        document.body.style.overflow = 'hidden';
        return () => {
            document.removeEventListener('keydown', onKey);
            document.body.style.overflow = prev;
        };
    }, [onClose]);

    return (
        <div
            role="dialog"
            aria-modal="true"
            onClick={onClose}
            style={{
                position: 'fixed',
                inset: 0,
                background: 'rgba(0,0,0,.45)',
                zIndex: 1000,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                padding: 24,
            }}
        >
            <div
                onClick={e => e.stopPropagation()}
                style={{
                    width: 'min(1200px, 96vw)',
                    height: 'min(86vh, 960px)',
                    background: '#fff',
                    borderRadius: 8,
                    boxShadow: '0 16px 40px rgba(0,0,0,.25)',
                    display: 'flex',
                    flexDirection: 'column',
                    overflow: 'hidden',
                }}
            >
                <div
                    style={{
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'space-between',
                        padding: '10px 14px',
                        borderBottom: '1px solid #e5e7eb',
                    }}
                >
                    <div style={{ fontWeight: 600 }}>{effectiveTitle}</div>
                    <button
                        onClick={onClose}
                        style={{
                            display: 'inline-flex',
                            alignItems: 'center',
                            gap: 6,
                            padding: '6px 10px',
                            border: '1px solid #d1d5da',
                            borderRadius: 6,
                            background: '#fff',
                            cursor: 'pointer',
                        }}
                    >
                        <FullScreenMinimizeRegular fontSize={16} /> {intl.formatMessage(SreAgentResources.close)}
                    </button>
                </div>
                <div style={{ flex: 1, overflow: 'auto' }}>{children}</div>
            </div>
        </div>
    );
};

// ---------- rebuild objects from path changes
const reconstructObjects = (changes: ParsedChanges) => {
    const beforeObj: any = {};
    const afterObj: any = {};
    const setNested = (obj: any, path: string, value: any) => {
        const keys = path.split('.');
        let cur = obj;
        for (let i = 0; i < keys.length - 1; i++) cur = cur[keys[i]] ??= {};
        if (value !== null && value !== undefined) cur[keys[keys.length - 1]] = value;
    };
    Object.entries(changes).forEach(([path, ch]) => {
        if (ch.previousValue !== null) setNested(beforeObj, path, ch.previousValue);
        if (ch.newValue !== null) setNested(afterObj, path, ch.newValue);
    });
    return { beforeObj, afterObj };
};

const FullSpecDiffView = ({
    changes,
    isFullscreen,
    changeMetadata,
}: {
    changes: ParsedChanges;
    isFullscreen: boolean;
    changeMetadata: { changedBy: string; changeTime: string; clientType: string; previousSnapshotId?: string; newSnapshotId?: string }[];
}) => {
    const [showSnapshots, setShowSnapshots] = useState(false);
    const [copiedSnapshot, setCopiedSnapshot] = useState<string | null>(null);

    // ✅ Hooks are called unconditionally, before any return
    const propertyPaths = useMemo(() => Object.keys(changes), [changes]);
    const intl = useIntl();
    const { beforeObj, afterObj } = useMemo(() => reconstructObjects(changes), [changes]);

    const beforeLines = JSON.stringify(beforeObj, null, 2).split('\n');
    const afterLines = JSON.stringify(afterObj, null, 2).split('\n');

    const { changedBefore, changedAfter } = useMemo(() => {
        const changedBefore = new Set<number>();
        const changedAfter = new Set<number>();
        const keys = Object.keys(changes).map(p => p.split('.').pop()!);
        beforeLines.forEach((line, i) => {
            if (keys.some(k => line.includes(`"${k}":`))) changedBefore.add(i);
        });
        afterLines.forEach((line, i) => {
            if (keys.some(k => line.includes(`"${k}":`))) changedAfter.add(i);
        });
        return { changedBefore, changedAfter };
    }, [changes, beforeLines, afterLines]);

    const copyToClipboard = (text: string, id: string) => {
        navigator.clipboard.writeText(text);
        setCopiedSnapshot(id);
        setTimeout(() => setCopiedSnapshot(null), 1800);
    };
    const fmt = (d: Date | string) => {
        try {
            return new Date(d).toLocaleString();
        } catch {
            return String(d);
        }
    };

    const uniqueUsers = Array.from(new Set(changeMetadata.map(m => m.changedBy).filter(Boolean)));
    const uniqueClients = Array.from(new Set(changeMetadata.map(m => m.clientType).filter(Boolean)));
    const times = changeMetadata.map(m => Date.parse(m.changeTime)).filter(n => !Number.isNaN(n));
    const start = times.length ? new Date(Math.min(...times)) : null;
    const end = times.length ? new Date(Math.max(...times)) : null;
    const timeLabel = start && end && start.getTime() !== end.getTime() ? `${fmt(start)} – ${fmt(end)}` : end ? fmt(end) : '—';

    const snapshots = changeMetadata.filter(m => m.previousSnapshotId || m.newSnapshotId);

    // After all hooks, you can early-return safely
    if (!propertyPaths.length) {
        return (
            <div
                style={{
                    padding: 10,
                    textAlign: 'center',
                    color: '#605e5c',
                    fontStyle: 'italic',
                    background: '#fff',
                    border: '1px solid #e1e4e8',
                    borderRadius: 6,
                }}
            >
                {intl.formatMessage(SreAgentResources.noPropertyChangesDetected)}
            </div>
        );
    }

    return (
        <div
            style={{
                border: '1px solid #d1d5da',
                borderRadius: 6,
                overflow: 'hidden',
                background: '#fff',
                fontFamily: 'SFMono-Regular, Consolas, "Liberation Mono", Menlo, monospace',
                fontSize: 12,
                height: isFullscreen ? 'calc(100% - 16px)' : 360,
                display: 'flex',
                flexDirection: 'column',
            }}
        >
            {/* COMPACT HEADER */}
            <div
                style={{
                    background: '#f6f8fa',
                    borderBottom: '1px solid #d1d5da',
                    padding: '6px 12px',
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                }}
            >
                <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                    <DocumentTextRegular fontSize={14} />
                    <div>
                        <div style={{ fontWeight: 600, fontSize: 13, color: '#24292e' }}>
                            {intl.formatMessage(SreAgentResources.configurationChanges)}
                        </div>
                        <div style={{ fontSize: 11, color: '#586069', marginTop: 2, display: 'flex', gap: 12, alignItems: 'center' }}>
                            <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
                                <PersonRegular fontSize={12} />
                                <span style={{ whiteSpace: 'nowrap' }}>{uniqueUsers.join(', ') || '—'}</span>
                            </span>
                            <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
                                <CalendarRegular fontSize={12} />
                                <span>{timeLabel}</span>
                            </span>
                            <span style={{ color: '#6a737d' }}>via {uniqueClients.join(', ') || '—'}</span>
                        </div>
                    </div>
                </div>

                {snapshots.length > 0 && (
                    <button
                        onClick={() => setShowSnapshots(!showSnapshots)}
                        style={{
                            display: 'inline-flex',
                            alignItems: 'center',
                            gap: 4,
                            padding: '3px 8px',
                            fontSize: 11,
                            color: '#586069',
                            background: 'transparent',
                            border: '1px solid #d1d5da',
                            borderRadius: 4,
                            cursor: 'pointer',
                        }}
                    >
                        {showSnapshots ? <ChevronUpRegular fontSize={12} /> : <ChevronDownRegular fontSize={12} />}
                        Snapshots ({snapshots.length})
                    </button>
                )}
            </div>

            {/* Snapshots (collapsed by default) */}
            {showSnapshots && snapshots.length > 0 && (
                <div style={{ background: '#f6f8fa', borderBottom: '1px solid #d1d5da', padding: '6px 12px', fontSize: 11 }}>
                    {snapshots.map((s, i) => (
                        <div
                            key={i}
                            style={{ display: 'grid', gridTemplateColumns: '70px 1fr auto', gap: 8, alignItems: 'center', marginBottom: 4 }}
                        >
                            {s.previousSnapshotId && (
                                <>
                                    <span style={{ color: '#d73a49', fontWeight: 600 }}>
                                        {intl.formatMessage(SreAgentResources.previous)}
                                    </span>
                                    <code
                                        style={{
                                            color: '#586069',
                                            background: '#fff',
                                            padding: '2px 6px',
                                            borderRadius: 3,
                                            border: '1px solid #e1e4e8',
                                            overflow: 'hidden',
                                            textOverflow: 'ellipsis',
                                            whiteSpace: 'nowrap',
                                        }}
                                    >
                                        {s.previousSnapshotId}
                                    </code>
                                    <button
                                        onClick={() => copyToClipboard(s.previousSnapshotId!, `prev-${i}`)}
                                        style={{
                                            padding: '2px 6px',
                                            background: 'transparent',
                                            border: '1px solid #d1d5da',
                                            borderRadius: 3,
                                            cursor: 'pointer',
                                            fontSize: 10,
                                            color: copiedSnapshot === `prev-${i}` ? '#28a745' : '#586069',
                                        }}
                                    >
                                        <CopyRegular fontSize={12} />
                                        {copiedSnapshot === `prev-${i}` ? 'Copied!' : 'Copy'}
                                    </button>
                                </>
                            )}
                            {s.newSnapshotId && (
                                <>
                                    <span style={{ color: '#28a745', fontWeight: 600 }}>
                                        {intl.formatMessage(SreAgentResources.current)}
                                    </span>
                                    <code
                                        style={{
                                            color: '#586069',
                                            background: '#fff',
                                            padding: '2px 6px',
                                            borderRadius: 3,
                                            border: '1px solid #e1e4e8',
                                            overflow: 'hidden',
                                            textOverflow: 'ellipsis',
                                            whiteSpace: 'nowrap',
                                        }}
                                    >
                                        {s.newSnapshotId}
                                    </code>
                                    <button
                                        onClick={() => copyToClipboard(s.newSnapshotId!, `new-${i}`)}
                                        style={{
                                            padding: '2px 6px',
                                            background: 'transparent',
                                            border: '1px solid #d1d5da',
                                            borderRadius: 3,
                                            cursor: 'pointer',
                                            fontSize: 10,
                                            color: copiedSnapshot === `new-${i}` ? '#28a745' : '#586069',
                                        }}
                                    >
                                        <CopyRegular fontSize={12} />
                                        {copiedSnapshot === `new-${i}` ? 'Copied!' : 'Copy'}
                                    </button>
                                </>
                            )}
                        </div>
                    ))}
                </div>
            )}

            {/* Split view */}
            <div style={{ display: 'flex', flex: 1, overflow: 'auto' }}>
                {/* Left (before) */}
                <div style={{ flex: 1, display: 'flex', borderRight: '1px solid #d1d5da' }}>
                    <div
                        style={{
                            background: '#f6f8fa',
                            borderRight: '1px solid #d1d5da',
                            padding: '6px 0',
                            minWidth: 44,
                            textAlign: 'right',
                            color: '#6a737d',
                            fontSize: 12,
                            lineHeight: '18px',
                            userSelect: 'none',
                        }}
                    >
                        {beforeLines.map((_, i) => (
                            <div key={i} style={{ padding: '0 6px', background: changedBefore.has(i) ? '#ffdce0' : 'transparent' }}>
                                {i + 1}
                            </div>
                        ))}
                    </div>
                    <div style={{ flex: 1, padding: '6px 0', overflow: 'auto' }}>
                        {beforeLines.map((line, i) => (
                            <div
                                key={i}
                                style={{
                                    padding: '0 10px',
                                    lineHeight: '18px',
                                    background: changedBefore.has(i) ? '#ffeef0' : 'transparent',
                                    color: changedBefore.has(i) ? '#cb2431' : '#24292e',
                                    wordBreak: 'break-all',
                                    whiteSpace: 'pre-wrap',
                                }}
                            >
                                {line}
                            </div>
                        ))}
                    </div>
                </div>

                {/* Right (after) */}
                <div style={{ flex: 1, display: 'flex' }}>
                    <div
                        style={{
                            background: '#f6f8fa',
                            borderRight: '1px solid #d1d5da',
                            padding: '6px 0',
                            minWidth: 44,
                            textAlign: 'right',
                            color: '#6a737d',
                            fontSize: 12,
                            lineHeight: '18px',
                            userSelect: 'none',
                        }}
                    >
                        {afterLines.map((_, i) => (
                            <div key={i} style={{ padding: '0 6px', background: changedAfter.has(i) ? '#cdffd8' : 'transparent' }}>
                                {i + 1}
                            </div>
                        ))}
                    </div>
                    <div style={{ flex: 1, padding: '6px 0', overflow: 'auto' }}>
                        {afterLines.map((line, i) => (
                            <div
                                key={i}
                                style={{
                                    padding: '0 10px',
                                    lineHeight: '18px',
                                    background: changedAfter.has(i) ? '#f0fff4' : 'transparent',
                                    color: changedAfter.has(i) ? '#28a745' : '#24292e',
                                    wordBreak: 'break-all',
                                    whiteSpace: 'pre-wrap',
                                }}
                            >
                                {line}
                            </div>
                        ))}
                    </div>
                </div>
            </div>
        </div>
    );
};

interface ChangeDiffViewer {
    id: string;
    title: string;
    description: string;
    correlationId: string;
    resourceId: string;
    changes: ChangeDiffItem[];
}

interface ChangeDiffMessageProps {
    changeDiffData: ChangeDiffViewer;
}

const ChangeDiffMessage = ({ changeDiffData }: ChangeDiffMessageProps) => {
    const [isFullscreen, setIsFullscreen] = useState(false);
    const intl = useIntl();

    const getChangeTypeStyle = (changeType: string) => {
        const t = changeType.toLowerCase();
        if (t.includes('create')) return { color: '#28a745', bg: '#28a74515' };
        if (t.includes('update') || t.includes('modify') || t.includes('write')) return { color: '#e36209', bg: '#e3620915' };
        if (t.includes('delete') || t.includes('remove')) return { color: '#d73a49', bg: '#d73a4915' };
        return { color: '#586069', bg: '#58606915' };
    };

    // Merge all changes into a single object for unified diff
    const mergedChanges: ParsedChanges = {};
    changeDiffData.changes.forEach(change => Object.assign(mergedChanges, parseChanges(change)));

    // metadata for header
    const changeMetadata = changeDiffData.changes.map(c => ({
        changedBy: c.changedBy,
        changeTime: c.changeTime,
        clientType: c.clientType,
        previousSnapshotId: c.previousSnapshotId,
        newSnapshotId: c.newSnapshotId,
    }));

    const uniqueChangeTypes = Array.from(new Set(changeDiffData.changes.map(c => c.changeType)));

    const core = (
        <>
            {/* Title row */}
            <div style={{ marginBottom: 12 }}>
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 8 }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                        <HistoryRegular fontSize={20} color="#0078d4" />
                        <h4 style={{ margin: 0, fontWeight: 600, fontSize: 16, color: '#323130' }}>{changeDiffData.title}</h4>
                    </div>
                    <button
                        onClick={() => setIsFullscreen(true)}
                        style={{
                            padding: '6px 10px',
                            background: '#ffffff',
                            border: '1px solid #d1d5da',
                            borderRadius: 6,
                            cursor: 'pointer',
                            display: 'flex',
                            alignItems: 'center',
                            gap: 6,
                            fontSize: 12,
                            color: '#24292e',
                        }}
                    >
                        <FullScreenMaximizeRegular fontSize={16} /> Fullscreen
                    </button>
                </div>

                <p style={{ margin: '0 0 10px 0', color: '#605e5c', fontSize: 14, lineHeight: 1.45 }}>{changeDiffData.description}</p>

                <div
                    style={{
                        background: '#fff',
                        border: '1px solid #e1e4e8',
                        borderRadius: 6,
                        padding: 8,
                        fontSize: 12,
                        fontFamily: 'SFMono-Regular, Consolas, monospace',
                    }}
                >
                    <div style={{ color: '#605e5c', marginBottom: 2 }}>
                        <strong>{intl.formatMessage(SreAgentResources.correlationIdLabel)}</strong>{' '}
                        <span style={{ color: '#323130' }}>{changeDiffData.correlationId}</span>
                    </div>
                    <div style={{ color: '#605e5c' }}>
                        <strong>Resource:</strong>{' '}
                        <span style={{ color: '#323130', wordBreak: 'break-all' }}>{changeDiffData.resourceId}</span>
                    </div>
                </div>
            </div>

            {/* Deduped change-type badges */}
            {uniqueChangeTypes.length > 0 && (
                <div style={{ marginBottom: 10 }}>
                    {uniqueChangeTypes.map((ct, i) => {
                        const s = getChangeTypeStyle(ct);
                        return (
                            <span
                                key={i}
                                style={{
                                    padding: '2px 8px',
                                    borderRadius: 12,
                                    background: s.bg,
                                    color: s.color,
                                    fontSize: 11,
                                    fontWeight: 600,
                                    textTransform: 'uppercase',
                                    letterSpacing: 0.5,
                                    marginRight: 6,
                                    display: 'inline-block',
                                }}
                            >
                                {ct}
                            </span>
                        );
                    })}
                </div>
            )}

            {/* Diff */}
            {Object.keys(mergedChanges).length > 0 ? (
                <FullSpecDiffView changes={mergedChanges} isFullscreen={false} changeMetadata={changeMetadata} />
            ) : (
                <div
                    style={{
                        textAlign: 'center',
                        padding: '20px 16px',
                        color: '#605e5c',
                        background: '#ffffff',
                        border: '1px solid #e1e4e8',
                        borderRadius: 6,
                        fontSize: 14,
                    }}
                >
                    {intl.formatMessage(SreAgentResources.noChangesFoundForCorrelation)}
                </div>
            )}
        </>
    );

    return (
        <>
            <div style={{ border: '1px solid #ececec', borderRadius: 8, padding: 14, marginTop: 16, background: '#f9f9f9' }}>{core}</div>

            {isFullscreen && (
                <Modal onClose={() => setIsFullscreen(false)} title={changeDiffData.title}>
                    <div style={{ padding: 16 }}>
                        <p style={{ margin: '0 0 10px 0', color: '#605e5c', fontSize: 14, lineHeight: 1.45 }}>
                            {changeDiffData.description}
                        </p>
                        <div
                            style={{
                                background: '#fff',
                                border: '1px solid #e1e4e8',
                                borderRadius: 6,
                                padding: 8,
                                fontSize: 12,
                                fontFamily: 'SFMono-Regular, Consolas, monospace',
                                marginBottom: 10,
                            }}
                        >
                            <div style={{ color: '#605e5c', marginBottom: 2 }}>
                                <strong>{intl.formatMessage(SreAgentResources.correlationIdLabel)}</strong>{' '}
                                <span style={{ color: '#323130' }}>{changeDiffData.correlationId}</span>
                            </div>
                            <div style={{ color: '#605e5c' }}>
                                <strong>Resource:</strong>{' '}
                                <span style={{ color: '#323130', wordBreak: 'break-all' }}>{changeDiffData.resourceId}</span>
                            </div>
                        </div>
                        <FullSpecDiffView changes={mergedChanges} isFullscreen={true} changeMetadata={changeMetadata} />
                    </div>
                </Modal>
            )}
        </>
    );
};

export default ChangeDiffMessage;
