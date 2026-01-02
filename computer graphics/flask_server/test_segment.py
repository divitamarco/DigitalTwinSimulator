import requests
import json

# URL del server Flask
url = "http://127.0.0.1:5000/segment"

# Percorso dell'immagine da testare
image_path = "test_images/oggetto5.jpg"  # Cambia con la tua immagine

with open(image_path, "rb") as f:
    files = {"image": f}
    response = requests.post(url, files=files)

try:
    result = response.json()
    print(json.dumps(result, indent=2))
except Exception as e:
    print("Errore nella risposta:", e)
    print(response.text)
