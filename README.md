# TTS-Company
A text-to-speech library mod for Lethal Company that integrates the lightweight, local [Piper TTS](https://github.com/OHF-Voice/piper1-gpl) engine. This library makes it easy for devs to add TTS speech to their mods, with generation happening fully offline on the player's machine.

> **All players in the lobby must have `TTS-Company` installed.** Players missing the mod will not hear any generated text-to-speech audio.

> Marked as `AI-Generated` cause of the AI TTS it produces.

## For devs
- **[Usage Guides](https://pixelindiedev.github.io/TTS-Company-docs/gettingstarted_docs.html)**
- **[API Documentation](https://pixelindiedev.github.io/TTS-Company-docs/api_docs.html)**
- **[Utils Documentation](https://pixelindiedev.github.io/TTS-Company-docs/utils_docs.html)**

## What it does
- Provides a API for other mods to generate local, low-latency text-to-speech audio.
- Spawns, manages, and prepares the underlying background Piper process so it's ready to handle audio requests.
- Uses the Piper TTS engine to generate text-to-speech directly on your machine without relying on external web APIs.
- Caches generated voice lines to significantly cut down on processing overhead and maximize in-game performance for recurring phrases.
- Gives developers the ability to pre-generate specific text-to-speech audio lines ahead of time to guarantee instant and seamless playback when needed.

## What it doesn't do
- **Does nothing on its own:** It solely initializes the background process and exposes the code API. It will not add chat commands, voice lines, or gameplay features unless paired with a mod that uses this library.
- **Does not send any data to external servers**: all audio synthesis is completely offline.

## License & Credits
This mod is licensed under the **GPL-3.0 License** due to its dependencies and bundled components.

### Third-Party Software & Libraries
This mod makes use of the following third-party software:

#### [Piper TTS](https://github.com/OHF-Voice/piper1-gpl)
- **License:** GPL-3.0 License
- **Usage:** Included as modified source code, compiled into a frozen server executable via PyInstaller.
- **Source Code:** The source code used to build the executable is included as a `.7z` archive inside `/PiperTTS`.
