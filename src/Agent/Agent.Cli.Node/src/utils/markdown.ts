/**
 * Markdown rendering utilities for terminal output
 */
import { marked } from 'marked';
import { markedTerminal } from 'marked-terminal';
import chalk from 'chalk';
import stripAnsi from 'strip-ansi';

// Configure marked-terminal options
const createMarkdownConfig = (terminalWidth?: number) => {
  return {
    // Headings
    heading: chalk.bold,
    firstHeading: chalk.whiteBright.bold,

    // Emphasis
    strong: chalk.bold,
    em: chalk.italic,
    del: chalk.strikethrough,

    // Horizontal rule
    hr: chalk.white,

    // Links
    link: chalk.cyanBright,
    href: chalk.cyanBright,

    // Images
    image: (href: string, _title?: string, text?: string) => {
      if (text) {
        return chalk.magentaBright(`Image: ${text} → ${href}`);
      }
      return chalk.magentaBright(`Image: ${href}`);
    },

    // Inline code / code block
    codespan: chalk.cyan,
    code: chalk.cyan,

    // List formatting - replace * with -
    list: (body: string, _ordered: boolean, indent: string) => {
      let result = body.replace(/^(\s*)\* /gm, '$1- ');

      // Remove base indent added by marked-terminal
      if (indent) {
        const lines = result.split('\n');
        const dedented = lines.map((line) =>
          line.startsWith(indent) ? line.slice(indent.length) : line
        );
        result = dedented.join('\n');
      }

      return result.trimEnd();
    },

    // Layout options
    reflowText: true,
    width: terminalWidth,
    showSectionPrefix: false,
    unescape: true,
    emoji: true,
    tab: 2,

    // Table options (cli-table3)
    tableOptions: {
      style: {
        compact: false,
      },
      wordWrap: true,
      wrapOnWordBoundary: true,
    },
  };
};

// Initialize marked with terminal renderer
let isInitialized = false;

export function initializeMarkdown(terminalWidth: number = 80): void {
  const XPADDING = 4;
  const availableWidth = Math.max(20, terminalWidth - XPADDING);
  marked.use(markedTerminal(createMarkdownConfig(availableWidth)));
  isInitialized = true;
}

// Ensure there are no chars that can interfere with box art
const BOX_DRAWING = /[\u2500-\u257F\u2580-\u259F]/g;

/**
 * Cleans terminal text by removing ANSI codes, converting line endings, and removing problematic characters
 */
export function cleanTerminalText(raw: string): string {
  return stripAnsi(raw).replace(/\r/g, '\n').replace(/\t/g, '    ').replace(BOX_DRAWING, '');
}

/**
 * Renders markdown to terminal-friendly text with fallback to plain text
 */
export function renderMarkdownToTerminal(markdown: string, terminalWidth?: number): string {
  // Initialize with default width if not done
  if (!isInitialized) {
    initializeMarkdown(terminalWidth);
  }

  try {
    const rendered = marked.parse(markdown, { async: false }) as string;
    // Clean the rendered output:
    // - Collapse 3+ consecutive newlines to 2 (max 1 blank line between elements)
    // - Trim leading/trailing whitespace
    return rendered.replace(/\n{3,}/g, '\n\n').trim();
  } catch (_error) {
    // Fallback to plain text if markdown parsing fails
    return cleanTerminalText(markdown);
  }
}

/**
 * Expands tabs into spaces in a multiline string
 */
export function expandTabs(str: string, tabSize: number = 4): string {
  if (!str.includes('\t')) {
    return str;
  }

  return str
    .split('\n')
    .map((line) => {
      let result = '';
      let col = 0;
      for (const char of line) {
        if (char === '\t') {
          const spaces = tabSize - (col % tabSize);
          result += ' '.repeat(spaces);
          col += spaces;
        } else {
          result += char;
          col += 1;
        }
      }
      return result;
    })
    .join('\n');
}
