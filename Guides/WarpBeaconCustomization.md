# Customizing warp beacons


Some warp parameters can now be set on the warp beacon item hieararchy.

Warping to a planet uses the parameters of the "WarpBeacon" entry in item
hierarchy.

## Requirements

An up-to-date client (1.4.3-mydu or higher) and server (1.4.9 or higher).


## Parameters

warpCellRate: rate of consumption of warp cells, Trip cost is defined as:

    wacpCellRate * MassInTon * distanceInMeters

minWarpDistance: minimum distance covered by the warp

maxWarpDistance: maximum distance covered by the warp

cruiseSpeedKmH: warp cruise travel speed

setupTimer: time in seconds before a warp beacon becomes active

The entries maxPeerBeaconDistance and requiredWarpDriveTypeNames are provisioned
for future use but not implemented yet.

## How to make a new warp beacon type:
    
Create a new child of WarpBeaconUnit. Here is a sample:

    TranswarpBeacon:
      parent: WarpBeaconUnit
      assetAlias: WarpBeacon
      cruiseSpeedKmH: 252000000
      warpCellRate: 0.0000025
      maxWarpDistance: 100000000000
      setupTimer: 10
      displayName: "Transwarp beacon"
      unitMass: 148943
      unitVolume: 25360
      level: 5
      scale: xl
      price: 23290.43
      hitpoints: 43117

