import requests
import json

# URL del server Flask
url = "http://127.0.0.1:5000/generate3d"

# Nome del file ritagliato restituito da /segment (solo il nome del file nella cartella 'static')
cutout_filename = "animal_character_2.png"  # Sostituisci con quello reale restituito da /segment

# Creiamo il payload per la richiesta
data = {
    "image_path": cutout_filename  # solo nome file, sarà cercato in 'static/'
}

# Invio della richiesta POST
response = requests.post(url, json=data)

# Gestione della risposta
try:
    result = response.json()
    print(json.dumps(result, indent=2))
except Exception as e:
    print("Errore nella risposta:", e)
    print(response.text)
