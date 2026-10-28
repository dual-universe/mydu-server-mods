# Forewords

Starting with server 1.5.10, the weapon hit/miss ratio default computation can be overridden with a custom formula.

# Enabling

Set PvPConfig.hitMissFormula in the item hierarchy to the mathematical formula. That formula must return a value between 0 and 1: the probability that the shot will hit.

# Available variables

The formula can make use of the following variables:

- accuracy: Weapon base accuracy times ammo modifier
- crossSectionFactor: precomputed cross-section factor
- angleFactor: precomputed angle factor (using optimal/falloff values)
- distanceFactor: precomputed distance factor (using optimal/falloff values)
- trackingFactor: precomputed tracking (angular velocity) factor
- optimalCS: optimal cross-section area, (optimalCrossSectionDiameter / 2) squared times PI
- crossSection: raw cross section
- trackingOptimalValue
- trackingFallOffValue
- angularVelocity
- distance
- distanceOptimalValue
- distanceFallOffValue
- angle
- angleOptimalValue
- angleFallOffValue
- baseAccuracy: weapon base accuracy
- accuracyModifier: ammo accuracy modifier
- weaponType: kind of the weapon

# Available functions

The mathematical expression is evaluated using the library linked below, refer to their documentations for available mathematical functions:

https://github.com/matheval/expression-evaluator-c-sharp