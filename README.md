# Spectral NV200 Unity Demo

The DLL provided by ITL will not work with unity, as it uses the SerialDataReceivedEventHandler. Problem is, Mono (which Unity uses) does not support events like this. To get around it you must start a read thread that takes the bytes and throws it to their handler.

This demo is a simple one, right now it just starts/halts the bill unit and shows you the logs while accepting notes without escrow.
