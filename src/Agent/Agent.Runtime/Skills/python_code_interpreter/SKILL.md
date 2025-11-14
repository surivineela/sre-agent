# Python Code Interpreter Skill

Execute Python safely in a sandbox to analyze/transform data and produce reusable files (PDF, images, CSV, Excel, text, etc.). Save files under /mnt/data with clear, descriptive filenames. Any saved file is automatically surfaced in chat and publicly accessible at /api/files/<filename> (e.g., /api/files/report.pdf).

## Core Capabilities

- Data wrangling and analysis using pandas and numpy; statistical summaries and calculations
- File generation: PDFs, images (PNG/JPG/SVG), CSV, Excel, Markdown, TXT
- Visualization with matplotlib first; use seaborn/plotly only if already available
- Report composition combining charts, tables, and narrative
- Light image operations via Pillow; basic scientific operations via scipy if available

## Tools

- ExecutePythonSnippet — run Python to compute and save files to /mnt/data
- GeneratePdfReport — run Python that outputs a single PDF (ensure script path and output name match)
- No manual file listing is needed; the runtime surfaces results automatically.

## Output and Artifact Guidance

- Use file artifacts when content is substantial (>15 lines), self-contained, intended for reuse (reports, decks, data exports, full scripts), or likely to be edited later.
- Prefer inline chat output for brief snippets, short explanations, or small examples.
- Produce one primary artifact per turn unless the user asks for multiple; if producing multiple files, include a small README.md.
- Include the complete content in generated files (avoid “...rest unchanged”). Produce polished, ready-to-use outputs.
- When helpful, add a short index section in the reply linking to /api/files/<filename> for each file.
- If asked for “an image,” consider SVG for crisp vector charts (plt.savefig('...svg')); otherwise use PNG.
- For HTML reports, prefer single-file HTML with inline CSS and data URIs for images (no external assets).
- Avoid creating hazardous or risky artifacts. If something would be unsafe, decline and suggest safer alternatives.
- For iterative versions, either overwrite the same filename or suffix with -v2, -v3, etc., for clarity.

## Public File Serving

- Any file saved to /mnt/data/NAME.ext is fetchable at /api/files/NAME.ext.
- Use simple, URL-safe names in kebab-case.
  - Example save path: /mnt/data/playground-insights-report.pdf
  - Example link: /api/files/playground-insights-report.pdf

## Security and Sandbox Constraints

- No network: do not make HTTP calls, scraping, or external API requests.
- No install/exec: no pip/conda/subprocess/shell; no OS-level changes.
- Filesystem: read/write only under /mnt/data.
- Avoid long-running loops; keep execution bounded.
- Disallowed modules (examples): requests, urllib, httpx, socket, ssl, paramiko, boto3, http.client, subprocess, os.system, pty, pip, conda, wget, curl.

## Self-Checks Before Finalizing

- Verify each expected file exists under /mnt/data and is non-empty.
- PDFs:
  - Compute available page width (page_width - left - right). Ensure sum(colWidths) ≤ available.
  - If not, recompute colWidths using the helper below, use Paragraphs for wrapping, and regenerate.
  - Consider landscape orientation or splitting wide tables across pages if columns ≥ 7.
- Images:
  - Ensure figure width fits ~7.5 inches (letter page). Use tight/constrained layout.
  - Save with dpi=150, bbox_inches='tight', then close the figure (plt.close()).
- Excel:
  - Set sensible column widths, freeze the header row, and ensure sheet names are < 31 chars.
- If fixes were applied, summarize adjustments briefly without revealing internal reasoning.

## PDF Width Auto-Fit Helpers (ReportLab)

Use these patterns to avoid table overflow. Keep code concise in final scripts.

```python
# --- sizing helpers ---
from reportlab.pdfbase.pdfmetrics import stringWidth
from reportlab.lib.pagesizes import letter, landscape
from reportlab.platypus import SimpleDocTemplate, Table, TableStyle, Paragraph, Spacer, PageBreak
from reportlab.lib.styles import getSampleStyleSheet

def autosize_col_widths(rows, max_width, font_name='Helvetica', font_size=9, min_col=48, max_col=240):
    """Compute reasonable column widths that fit within max_width.
    rows: list[list[str]] of plain strings (convert to Paragraphs after sizing)."""
    if not rows or not rows[0]:
        return []
    cols = len(rows[0])
    natural = [min_col] * cols
    pad = 10  # small padding per cell
    for row in rows:
        for c, cell in enumerate(row[:cols]):
            txt = str(cell)
            try:
                w = stringWidth(txt, font_name, font_size) + pad
            except Exception:
                w = min_col
            if w > natural[c]:
                natural[c] = min(w, max_col)
    total = sum(natural)
    if total == 0:
        return [max_width / cols] * cols
    scale = min(1.0, max_width / total)
    col_widths = [max(min_col, min(max_col, w * scale)) for w in natural]
    # Normalize last column so the sum matches max_width exactly
    delta = max_width - sum(col_widths)
    col_widths[-1] += delta
    return col_widths

def to_paragraph_rows(rows, style=None):
    style = style or getSampleStyleSheet()['BodyText']
    return [[Paragraph(str(cell), style) for cell in row] for row in rows]
```

Minimal usage sketch:

```python
from reportlab.lib.units import inch
from reportlab.platypus import SimpleDocTemplate, Table, TableStyle
from reportlab.lib.pagesizes import letter, landscape
from reportlab.lib import colors
from reportlab.lib.styles import getSampleStyleSheet

raw_rows = data  # list of rows (first row is header)
pagesize = landscape(letter) if len(raw_rows[0]) >= 7 else letter

left, right, top, bottom = 0.75*inch, 0.75*inch, 1.0*inch, 1.0*inch
page_w = pagesize[0]
available_w = page_w - left - right

colWidths = autosize_col_widths(raw_rows, available_w)
styles = getSampleStyleSheet()
para_rows = to_paragraph_rows(raw_rows, styles['BodyText'])

doc = SimpleDocTemplate('/mnt/data/report.pdf', pagesize=pagesize,
                        leftMargin=left, rightMargin=right,
                        topMargin=top, bottomMargin=bottom)

table = Table(para_rows, colWidths=colWidths, repeatRows=1)
table.setStyle(TableStyle([
    ('GRID', (0,0), (-1,-1), 0.5, colors.grey),
    ('FONTNAME', (0,0), (-1,0), 'Helvetica-Bold'),
    ('ALIGN', (0,0), (-1,0), 'CENTER'),
    ('VALIGN', (0,0), (-1,-1), 'TOP'),
    ('FONTSIZE', (0,0), (-1,-1), 9),
]))

doc.build([table])
```

## File Workflow (Automatic)

- Save outputs to /mnt/data and return. The runtime will:
  1) Detect all new files in /mnt/data
  2) Render images inline
  3) Provide download links
  4) Additionally, users can GET the same files via /api/files/<filename>

## Code Style and Quality

- Write clean, commented code with graceful error handling and clear messages.
- Prefer deterministic, efficient operations; close figures after saving.
- Use only preinstalled libraries (subject to availability): pandas, numpy, matplotlib, seaborn, scipy, openpyxl, reportlab, Pillow, pdfplumber, pypdf.
- Images: plt.savefig('/mnt/data/name.png', dpi=150, bbox_inches='tight'); plt.close()
- CSV: df.to_csv('/mnt/data/name.csv', index=False)
- Excel: use ExcelWriter, adjust column widths, and freeze the header row.

## Examples

### 1) Quick data summary to CSV and Excel

```python
import pandas as pd

# Example dataframe
df = pd.DataFrame({
    'category': ['A','A','B','B','C'],
    'value': [10, 12, 7, 9, 15]
})

summary = df.groupby('category', as_index=False)['value'].agg(['count','mean','min','max']).reset_index()
summary.columns = ['category','count','mean','min','max']

# Save CSV
csv_path = '/mnt/data/category-summary.csv'
summary.to_csv(csv_path, index=False)

# Save Excel with formatting
excel_path = '/mnt/data/category-summary.xlsx'
with pd.ExcelWriter(excel_path, engine='openpyxl') as writer:
    summary.to_excel(writer, index=False, sheet_name='Summary')
    ws = writer.book['Summary']
    # Freeze header
    ws.freeze_panes = 'A2'
    # Auto-width (basic heuristic)
    for col in ws.columns:
        maxlen = max(len(str(c.value)) if c.value is not None else 0 for c in col)
        ws.column_dimensions[col[0].column_letter].width = min(max(10, maxlen + 2), 40)

print(csv_path, excel_path)
```

### 2) Basic matplotlib chart (PNG and SVG)

```python
import matplotlib.pyplot as plt

vals = [10, 12, 7, 9, 15]
cats = ['A','A2','B','B2','C']

plt.figure(figsize=(6, 3.5))
plt.plot(cats, vals, marker='o')
plt.title('Sample Trend')
plt.xlabel('Category')
plt.ylabel('Value')
plt.grid(True, alpha=0.3)
png_path = '/mnt/data/sample-trend.png'
svg_path = '/mnt/data/sample-trend.svg'
plt.savefig(png_path, dpi=150, bbox_inches='tight')
plt.savefig(svg_path, bbox_inches='tight')  # crisp vector
plt.close()

print(png_path, svg_path)
```

### 3) Single-file HTML report with inline image

```python
import base64

png_path = '/mnt/data/sample-trend.png'  # assume created above
with open(png_path, 'rb') as f:
    b64 = base64.b64encode(f.read()).decode('ascii')
img_tag = f'<img alt="Trend" src="data:image/png;base64,{b64}" style="max-width:100%;height:auto;" />'

html = f"""<!doctype html>
<html>
<head>
<meta charset="utf-8" />
<title>Sample Report</title>
<style>
  body {{ font-family: system-ui, -apple-system, Segoe UI, Roboto, Arial, sans-serif; margin: 24px; }}
  h1 {{ margin-top: 0; }}
  table {{ border-collapse: collapse; width: 100%; }}
  th, td {{ border: 1px solid #ccc; padding: 6px 8px; text-align: left; }}
</style>
</head>
<body>
  <h1>Sample Report</h1>
  <p>This is a single-file HTML report with an embedded image.</p>
  {img_tag}
</body>
</html>"""

html_path = '/mnt/data/sample-report.html'
with open(html_path, 'w', encoding='utf-8') as f:
    f.write(html)

print(html_path)
```

### 4) PDF table using the auto-fit helpers

```python
from reportlab.lib.pagesizes import letter, landscape
from reportlab.lib.units import inch
from reportlab.platypus import SimpleDocTemplate, Table, TableStyle, Paragraph, Spacer
from reportlab.lib import colors
from reportlab.lib.styles import getSampleStyleSheet

# Example tabular data
rows = [
    ['Category','Count','Mean','Min','Max'],
    ['A',  2, 11.0, 10, 12],
    ['B',  2,  8.0,  7,  9],
    ['C',  1, 15.0, 15, 15],
]

pagesize = letter if len(rows[0]) < 7 else landscape(letter)
left, right, top, bottom = 0.75*inch, 0.75*inch, 1.0*inch, 1.0*inch
available_w = pagesize[0] - left - right

colWidths = autosize_col_widths(rows, available_w)
styles = getSampleStyleSheet()
para_rows = to_paragraph_rows(rows, styles['BodyText'])

pdf_path = '/mnt/data/summary-table.pdf'
doc = SimpleDocTemplate(pdf_path, pagesize=pagesize,
                        leftMargin=left, rightMargin=right,
                        topMargin=top, bottomMargin=bottom)

table = Table(para_rows, colWidths=colWidths, repeatRows=1)
table.setStyle(TableStyle([
    ('GRID', (0,0), (-1,-1), 0.5, colors.grey),
    ('FONTNAME', (0,0), (-1,0), 'Helvetica-Bold'),
    ('ALIGN', (0,0), (-1,0), 'CENTER'),
    ('VALIGN', (0,0), (-1,-1), 'TOP'),
    ('FONTSIZE', (0,0), (-1,-1), 9),
]))

doc.build([Paragraph('Summary Table', styles['Heading2']), Spacer(1, 6), table])
print(pdf_path)
```
