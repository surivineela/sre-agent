from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
import subprocess
import base64
import os
import asyncio
from playwright.async_api import async_playwright

app = FastAPI()

class MermaidSpec(BaseModel):
    spec: str

class ScreenshotRequest(BaseModel):
    grafanaEndpoint: str
    grafanaToken: str
    dashboardUrl: str

PERMANENT_DIR = "/app/tmp"
os.makedirs(PERMANENT_DIR, exist_ok=True)

@app.post("/render")
async def render_mermaid(payload: MermaidSpec):
    try:
        input_file = os.path.join(PERMANENT_DIR, "diagram.mmd")
        output_file = os.path.join(PERMANENT_DIR, "diagram.png")
        
        with open(input_file, "w") as f:
            f.write(payload.spec)
        
        subprocess.run([
            "mmdc",
            "-i", input_file,
            "-o", output_file,
            "--puppeteerConfigFile", "/app/puppeteer-config.json"
        ], check=True)
        
        # Read and encode the generated image.
        with open(output_file, "rb") as img_file:
            encoded_image = base64.b64encode(img_file.read()).decode("utf-8")
            
        return {"image_base64": encoded_image}
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

async def auto_scroll(page):
    max_scroll = 5
    viewport = page.viewport_size
    middle_x = viewport['width'] // 2
    middle_y = viewport['height'] // 2
    await page.mouse.click(middle_x, middle_y)

    for _ in range(max_scroll):
        await page.keyboard.press("PageDown")
        await page.wait_for_load_state("networkidle")

async def get_grafana_screenshot_async(grafana_endpoint, grafana_token, dashboard_url):
    print("Starting Grafana screenshot process")

    async with async_playwright() as p:
        browser = await p.webkit.launch(headless=True)

        page = await browser.new_page()
        navigate_url = f"{grafana_endpoint}{dashboard_url}"

        await page.set_viewport_size({"width": 1920, "height": 1080})
        await page.set_extra_http_headers({"Authorization": f"Bearer {grafana_token}"})

        print(f"Navigating to {navigate_url}")
        await page.goto(navigate_url, wait_until="networkidle")

        await page.wait_for_load_state("networkidle")

        dashboard_selector = "#page-scrollbar > div"
        await page.wait_for_selector(dashboard_selector)

        await auto_scroll(page)

        element = await page.query_selector(dashboard_selector)
        bounding_box = await element.bounding_box()

        print(f"Setting new viewport to be {bounding_box['width']} x {bounding_box['height']}")

        await page.set_viewport_size({
            "width": int(bounding_box['width']),
            "height": int(bounding_box['height']) + 150
        })


        await page.evaluate('''() => {
            const container = document.querySelector("#page-scrollbar > div");
            if (container) {
                const collapsedButtons = container.querySelectorAll('[aria-expanded="false"]');
                collapsedButtons.forEach((button) => button.click());
            }
        }''')

        dashboard_selector = "#page-scrollbar > div"
        await page.wait_for_selector(dashboard_selector)

        await auto_scroll(page)

        element = await page.query_selector(dashboard_selector)
        bounding_box = await element.bounding_box()

        print(f"Setting new viewport to be {bounding_box['width']} x {bounding_box['height']}")

        await page.set_viewport_size({
            "width": int(bounding_box['width']),
            "height": int(bounding_box['height']) + 150
        })

        await asyncio.sleep(8)

        print("Taking screenshot")
        screenshot = await page.screenshot(full_page=True)
        base64_screenshot = base64.b64encode(screenshot).decode("utf-8")

        await browser.close()
        return base64_screenshot

@app.post("/screenshot")
async def screenshot(request: ScreenshotRequest):
    if not request.grafanaEndpoint or not request.grafanaToken or not request.dashboardUrl:
        raise HTTPException(status_code=400, detail="Missing required fields")

    try:
        screenshot = await get_grafana_screenshot_async(
            request.grafanaEndpoint,
            request.grafanaToken,
            request.dashboardUrl
        )
        return {"screenshot": screenshot}
    except Exception as e:
        print(e)
        raise HTTPException(status_code=500, detail="Error taking screenshot")
