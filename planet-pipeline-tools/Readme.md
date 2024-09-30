# Tooling to manipulate planet pipeline

Run the following operations to change the undeground ore seed of a legacy planet named "MyInputPlanet.json":

Extract pipeline lz4

    python extractpipeline.py MyInputPlanet.json pipeline.lz4

Decompress pipeline

    lz4d pipeline.lz4 >pipeline.json

Change seed (use any value in place of 42, current value was likely 0)

    python reseed.py pipeline.json pipeline42.json 42

Compress pipeline

    lz4c <pipeline42.json >pipeline42.lz4

Inject pipeline in new planet json

    python replacepipeline.py MyInputPlanet.json pipeline42.lz4 MyNewReseededPlanet.json