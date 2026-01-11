# Overview

Il progetto **Digital Twin Simulator** è una piattaforma di **prototipazione rapida di Digital Twin 3D** che integra **Computer Graphics**, **Deep Learning** e **Mixed Reality**.
L’obiettivo è permettere la **generazione automatica di modelli 3D a partire da immagini 2D**, la loro **visualizzazione interattiva in Unity**, e l’**interazione tramite XR (grab, move, inspect)**.

Il sistema è pensato come una **pipeline end-to-end** che collega:

* segmentazione dell’immagine (SAM),
* generazione del modello 3D (Stable Fast 3D),
* rendering e interazione in ambiente Unity (XR).

---

## Pipeline di funzionamento

1. **Selezione immagine 2D**

   * L’utente seleziona un’immagine tramite interfaccia Unity.

2. **Segmentazione**

   * L’immagine viene inviata al backend Flask.
   * Il modello **SAM (Segment Anything Model)** individua l’oggetto principale.

3. **Generazione del modello 3D**

   * Stable Fast 3D genera un modello tridimensionale coerente.
   * Il modello viene esportato in formato **GLB**.

4. **Caricamento in Unity**

   * Unity carica dinamicamente il GLB.
   * Il modello viene inserito nella gerarchia senza distruggere i prefab XR.

5. **Interazione XR**

   * Il modello è immediatamente **grabbable** e manipolabile.
   * Supporto a Ray / Hand Interaction.

---

## Tecnologie utilizzate

| Componente           | Tecnologia                   |
| -------------------- | ---------------------------- |
| **Game Engine**      | Unity                        |
| **Frontend**         | C#                           |
| **Backend**          | Python (Flask)               |
| **Segmentazione**    | SAM (Segment Anything Model) |
| **Generazione 3D**   | Stable Fast 3D               |
| **Formato Modelli**  | GLB / glTF                   |
| **XR / Interaction** | Meta XR SDK                  |

---

## Struttura delle cartelle

```plaintext
computer_graphics/
├── flask_server/
│   ├── app.py
│   ├── sam_utils.py
│   ├── stable3d_utils.py
│   ├── requirements.txt
│   └── models/
│       └── sam_vit_h_4b8939.pth
├── stable-fast-3d/
│   ├── texture_baker/
│   ├── uv_unwrapper/
│   └── requirements.txt
├── unity_project/
│   ├── Scenes/
│   │   ├── DigitalTwin.unity
│   │   └── Scripts/
│   │       └── TwinWorkflow.cs
└── README.md
```

---

## Modelli di Deep Learning utilizzati

Il progetto utilizza **due modelli di Deep Learning distinti**, integrati in una pipeline sequenziale:

1. **SAM – Segment Anything Model** per la segmentazione dell’oggetto.
2. **Stable Fast 3D** per la generazione automatica del modello tridimensionale.

Entrambi i modelli sono eseguiti **localmente**, senza dipendenze da servizi cloud esterni.

---

## Segment Anything Model (SAM)

Il **Segment Anything Model (SAM)** viene utilizzato per isolare automaticamente l’oggetto di interesse all’interno dell’immagine 2D di input.
La segmentazione migliora significativamente la qualità della ricostruzione 3D, riducendo il rumore introdotto dallo sfondo.

### Repository ufficiale

🔗 [https://huggingface.co/HCMUE-Research/SAM-vit-h/blob/main/sam_vit_h_4b8939.pth]

## Stable Fast 3D – Generazione del modello 3D

La ricostruzione tridimensionale avviene tramite **Stable Fast 3D**, un framework sviluppato da **Stability AI** per la generazione rapida di mesh 3D a partire da immagini segmentate.

### Repository ufficiale

🔗 [https://github.com/Stability-AI/stable-fast-3d](https://github.com/Stability-AI/stable-fast-3d)

### Installazione di Stable Fast 3D

> **NB:** Stable Fast 3D richiede **Python 3.9** (testato).
> Versioni successive possono causare errori di build su Windows.

> **NB:** Stable Fast 3D richiede il login a HuggingFace: [https://huggingface.co]

> Crea un token di accesso con permessi di lettura.

> Esegui ```bash huggingface-cli login ``` nell’ambiente e inserisci il token.

1. Clonare il repository:

```bash
git clone https://github.com/Stability-AI/stable-fast-3d.git
cd stable-fast-3d
```

2. Creare e attivare l’ambiente virtuale:

```bash
python3.9 -m venv venv
source venv/bin/activate   # Linux / WSL
venv\Scripts\activate      # Windows
```

3. Installare PyTorch (obbligatorio):

```bash
pip install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu121
```

4. Installare le dipendenze:

```bash
pip install -r requirements.txt --no-build-isolation
```

L’output della pipeline è un file **GLB**, successivamente caricato dinamicamente in Unity.

---

## Setup del Backend (Flask + Stable Fast 3D)

> **NB:** Il backend è stato sviluppato e testato su **Windows**, con Python **3.9**.
> **NB:** Il frontend è stato sviluppato e testato su **Unity**, con Meta XR SDK **77.0.0**.
> Versioni più recenti potrebbero causare problemi di compatibilità.

1️⃣ Creazione ambiente virtuale

```bash
python3.9 -m venv venv
```

2️⃣ Installazione PyTorch

```bash
pip install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu121
```

3️⃣ Installazione dipendenze

```bash
pip install -r requirements.txt --no-build-isolation
```

4️⃣ Avvio server Flask

```bash
python app.py
```

Backend disponibile su:

```
http://127.0.0.1:5000
```

---

## Team

| Nome               | GitHub                                       |
| ------------------ | -------------------------------------------- |
| 👨 `Di Vita Marco` | [Click here](https://github.com/divitamarco) |

---

