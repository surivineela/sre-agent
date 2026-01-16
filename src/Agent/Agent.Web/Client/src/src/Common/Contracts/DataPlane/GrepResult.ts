/**
 * Structured result from a grep search operation for rich UI rendering.
 */
export interface GrepSearchResult {
    /** Total number of matches found across all files */
    totalMatches: number;
    /** The search query used */
    query: string;
    /** Whether the query was a regex pattern */
    isRegex: boolean;
    /** Grouped results by file */
    files: GrepFileResult[];
    /** Whether results were truncated due to limit */
    isTruncated: boolean;
    /** The maximum results limit that was applied */
    maxResults: number;
}

/**
 * Results for a single file in a grep search.
 */
export interface GrepFileResult {
    /** Relative path to the file from sandbox root */
    filePath: string;
    /** Number of matches in this file */
    matchCount: number;
    /** Individual line matches with context */
    matches: GrepLineMatch[];
}

/**
 * A single line match or context line in grep results.
 */
export interface GrepLineMatch {
    /** 1-based line number of the match */
    lineNumber: number;
    /** The full line content */
    content: string;
    /** Whether this is a context line (not a match line) */
    isContext: boolean;
    /** Start and end positions of matches within the line (for highlighting) */
    matchRanges: MatchRange[];
}

/**
 * Character range for highlighting a match within a line.
 */
export interface MatchRange {
    /** 0-based start character index */
    start: number;
    /** 0-based end character index (exclusive) */
    end: number;
}
