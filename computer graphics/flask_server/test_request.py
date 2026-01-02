import requests

url = "http://127.0.0.1:5000/segment"
image_path = "test_images/oggetto5.jpg"

with open(image_path, "rb") as f:
    files = {"image": f}
    response = requests.post(url, files=files)

print("Risposta server:", response.json())
