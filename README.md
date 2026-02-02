# CAT-Pharmacy 💊🔬

A high-performance, professional mastery engine designed to transform pharmacy school content into interactive, adaptive learning experiences. Built with a powerful Electron frontend and an intelligent Python-backed IRT engine.

## 🚀 Key Features

*   **Intelligent Slide Ingestion:** Automatically extracts Knowledge Units and learning objectives from lecture PPTX files using our custom Python parser.
*   **Recursive Discovery Algorithm:** An adaptive learning loop that prioritizes the "next best thing" for you to study based on real-time mastery signals.
*   **Brilliant-style Interactive Models:** Visualize and manipulate complex pharmacokinetic and pharmacology models with real-time SVG graphing.
*   **Mastery Tracking:** Deep-dive into your Knowledge Graph with live statistics on concept proficiency and study progress.
*   **Desktop Optimized:** A sleek 16:9 widescreen experience designed for high-focus study sessions.

## 🏗️ Architecture

VidTok is built with a state-of-the-art hybrid architecture:

*   **Frontend (Electron/Node.js):** Manages the professional desktop UI, local storage, and the IPC bridge.
*   **Backend (Python Engine):** Handles the mathematical heavy lifting, including Item Response Theory (IRT) calculations, PPTX parsing (`python-pptx`), and knowledge graph modeling.
*   **Persistence:** Local JSON-based datastore for knowledge units, graphs, and mastery states.

## 🛠️ Setup & Installation

### Prerequisites
*   **Node.js** (v20+)
*   **Python** (3.10+)
*   **python-pptx** (Installed via pip)

### Installation
1.  Clone the repository:
    ```bash
    git clone https://github.com/Baker-Harrison/CAT-Pharmacy.git
    cd CAT-Pharmacy
    ```
2.  Install Node dependencies:
    ```bash
    npm install
    ```
3.  Install Python dependencies:
    ```bash
    pip install -r backend/requirements.txt
    ```

## 🏃 Running the App

To launch the CAT-Pharmacy mastery engine:
```bash
npm start
```

## 🧪 Testing

The project maintains high stability through dual-layer testing:

*   **Renderer Tests (Jest):** `npm test`
*   **Backend Tests (Pytest):** `PYTHONPATH=. pytest backend/tests`

## 🤝 Maintenance

Maintained by **Baker-Harrison**. This project is a complete replacement for the legacy C# version.
