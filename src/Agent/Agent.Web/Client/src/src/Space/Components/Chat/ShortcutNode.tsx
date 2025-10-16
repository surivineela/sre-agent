import {
    $applyNodeReplacement,
    DecoratorNode,
    EditorConfig,
    LexicalNode,
    NodeKey,
    SerializedLexicalNode,
} from '@fluentui-copilot/react-copilot';
import { tokens } from '@fluentui/react-components';
import { Shortcut } from '../../Contracts/Activities';

export type ShortcutNodeType = 'shortcut';

export type SerializedShortcutNode = SerializedLexicalNode & {
    type: ShortcutNodeType;
    version: 1;
    text: string;
    shortcut: Shortcut;
};

export class ShortcutNode extends DecoratorNode<JSX.Element> {
    __text: string;
    __shortcut: Shortcut;

    static clone(node: ShortcutNode): ShortcutNode {
        return new ShortcutNode(node.__text, node.__shortcut, node.__key);
    }

    constructor(text: string, shortcut: Shortcut, key?: NodeKey) {
        super(key);
        this.__text = text;
        this.__shortcut = shortcut;
    }

    static getType(): string {
        return 'shortcut';
    }

    getTextContent(): string {
        return this.__text;
    }

    createDOM(_config: EditorConfig): HTMLElement {
        const dom = document.createElement('span');
        // Lexical manages lifecycle; the actual content will come from React
        return dom;
    }

    updateDOM(): boolean {
        return true; // React handles updates
    }

    // This is the key method for decorated nodes
    decorate(): JSX.Element {
        return (
            <span
                style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: tokens.spacingHorizontalXS,
                }}
            >
                <span
                    style={{
                        backgroundColor: tokens.colorBrandBackground2,
                        color: tokens.colorBrandForeground2,
                        padding: `${tokens.spacingVerticalXXS} ${tokens.spacingHorizontalXS}`,
                        borderRadius: tokens.borderRadiusSmall,
                        fontSize: tokens.fontSizeBase300,
                        fontWeight: tokens.fontWeightMedium,
                    }}
                >
                    {this.__text}
                </span>
            </span>
        );
    }

    // Required for DecoratorNode
    isInline(): boolean {
        return true;
    }

    isIsolated(): boolean {
        return true;
    }

    // When caret moves left into this node, collapse before it
    collapseAtStart(): boolean {
        return true;
    }

    // When caret moves right into this node, collapse after it
    collapseAtEnd(): boolean {
        return true;
    }

    // Serialization
    exportJSON(): SerializedShortcutNode {
        return {
            ...super.exportJSON(),
            type: 'shortcut',
            version: 1,
            text: this.__text,
            shortcut: this.__shortcut,
        };
    }

    static importJSON(serializedNode: SerializedShortcutNode): ShortcutNode {
        const node = $createShortcutNode(serializedNode.shortcut);
        return node;
    }

    // Update methods
    setText(text: string): void {
        const writable = this.getWritable();
        writable.__text = text;
    }

    setShortcut(shortcut: Shortcut): void {
        const writable = this.getWritable();
        writable.__shortcut = shortcut;
    }
}

// Factory helper
export function $createShortcutNode(shortcut: Shortcut): ShortcutNode {
    const node = new ShortcutNode(`/${shortcut}`, shortcut);
    return $applyNodeReplacement(node);
}

// Type guard
export function $isShortcutNode(node: LexicalNode | null | undefined): node is ShortcutNode {
    return node instanceof ShortcutNode;
}

export function $getShortcutValuefromShortcutNode(node: LexicalNode | null | undefined): Shortcut | null {
    if ($isShortcutNode(node)) {
        return node.__shortcut;
    }
    return null;
}
