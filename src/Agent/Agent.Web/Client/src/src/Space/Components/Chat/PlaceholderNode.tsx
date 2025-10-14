import {
    $applyNodeReplacement,
    DecoratorNode,
    EditorConfig,
    LexicalNode,
    NodeKey,
    SerializedLexicalNode,
} from '@fluentui-copilot/react-copilot';
import { tokens } from '@fluentui/react-components';

export type Placeholder = 'placeholder';

// Correct serialization type for DecoratorNode
export type SerializedPlaceholderNode = SerializedLexicalNode & {
    type: Placeholder;
    version: 1;
    placeholder: string;
};

export class PlaceholderNode extends DecoratorNode<JSX.Element> {
    __placeholder: string;

    static clone(node: PlaceholderNode): PlaceholderNode {
        return new PlaceholderNode(node.__placeholder, node.__key);
    }

    constructor(placeholder: string, key?: NodeKey) {
        super(key);
        this.__placeholder = placeholder;
    }

    static getType(): string {
        return 'placeholder';
    }

    getTextContent(): string {
        return '';
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
                    opacity: 0.6,
                    fontStyle: 'italic',
                    color: tokens.colorNeutralForeground3,
                    pointerEvents: 'none',
                    userSelect: 'none',
                }}
                contentEditable={false}
            >
                {this.__placeholder}
            </span>
        );
    }

    // Required for DecoratorNode
    isInline(): boolean {
        return true;
    }

    // Serialization
    exportJSON(): SerializedPlaceholderNode {
        return {
            ...super.exportJSON(),
            type: 'placeholder',
            version: 1,
            placeholder: this.__placeholder,
        };
    }

    static importJSON(serializedNode: SerializedPlaceholderNode): PlaceholderNode {
        const node = new PlaceholderNode(serializedNode.placeholder);
        return node;
    }

    // Update methods
    setPlaceholder(placeholder: string): void {
        const writable = this.getWritable();
        writable.__placeholder = placeholder;
    }
}

// Factory helper
export function $createPlaceholderNode(placeholder: string): PlaceholderNode {
    const node = new PlaceholderNode(placeholder);
    return $applyNodeReplacement(node);
}

// Type guard
export function $isPlaceholderNode(node: LexicalNode | null | undefined): node is PlaceholderNode {
    return node instanceof PlaceholderNode;
}
