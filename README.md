# TTS-Company
A text-to-speech library mod for Lethal Company that integrates the lightweight, local Piper TTS engine. This library makes it easy to add TTS speech to your Lethal Company mods.

> All players in the lobby are required to have the `TTS-Company` mod installed. Any players missing the mod will not be able to hear any generated text-to-speech audio.

## What it does
- Provides a API for other mods to generate local, low-latency text-to-speech audio.
- Spawns, manages, and prepares the underlying background Piper process so it's ready to handle audio requests.
- Uses the Piper TTS engine to generate text-to-speech directly on your machine without relying on external web APIs.
- Caches generated voice lines to significantly cut down on processing overhead and maximize in-game performance for recurring phrases
- Gives developers the ability to pre-generate specific text-to-speech audio lines ahead of time to guarantee instant and seamless playback when needed

## What it doesn't do
- **Does nothing on its own:** It solely initializes the background process and exposes the code API. It will not add chat commands, voice lines, or gameplay features unless paired with a mod that uses this library
- **Does not send any data to external servers**: all audio synthesis is completely offline

## License & Credits
This mod includes and utilizes the **Piper TTS** source code, modified and compiled into a frozen server executable via PyInstaller. 
- **Piper TTS** is licensed under the **GPL-3.0 License**
