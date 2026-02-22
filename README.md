
DSP Sound Card Audio Router
==========================

DSP Sound Card Audio Router is a lightweight tool to route audio from one ASIO interface to another — specifically designed to work with the DSP mix output of sound cards like Creative X-Fi.

Key Features
- Route the DSP mix output of your sound card directly to an HDMI device
- Perfect for sending 5.1/7.1 surround sound from an X-Fi to an HDMI receiver
- Supports lossless surround sound with up to 8 channels on X-Fi Titanium
- Uses low-latency ASIO for reliable performance

Why Use This?
If you own a Creative X-Fi or similar sound card with hardware DSP processing, you know the DSP mix output contains all post-processed audio (EQ, Crystalizer, etc.).  
This tool lets you capture that processed signal and send it to an external device — like an AV receiver — without losing surround channels or quality.

Project Status
Not in active development

Recommendations
To avoid clipping when routing the DSP output:

- Enable the X-Fi Equalizer
- Set the pre-amp level to a lower value:
  - –12 dB for general use
  - –24 dB if Crystalizer is enabled with high settings

This helps keep the signal clean before it reaches the HDMI output.
