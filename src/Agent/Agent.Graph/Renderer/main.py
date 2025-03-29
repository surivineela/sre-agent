from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
import subprocess
import base64
import os

app = FastAPI()

class MermaidSpec(BaseModel):
    spec: str

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
