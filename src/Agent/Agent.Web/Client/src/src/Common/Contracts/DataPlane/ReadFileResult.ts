/**
 * Structured result from a file read operation for rich UI rendering.
 */
export interface ReadFileResult {
    /** Relative path to the file from sandbox root */
    filePath: string;
    /** 1-based start line number */
    startLine: number;
    /** 1-based end line number (inclusive) */
    endLine: number;
    /** Total number of lines in the file */
    totalLines: number;
    /** The file content (lines joined with \n) */
    content: string;
    /** Whether the content was truncated due to line limit */
    isTruncated: boolean;
    /** Error message if the read failed */
    error?: string;
}
