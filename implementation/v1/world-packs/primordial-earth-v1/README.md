# Primordial Earth v1 generated world pack

This package is the first executable `32 × 17` primordial-Earth-like world
profile. It configures the version-one deterministic generator rather than
embedding generated tiles. The root world seed controls terrain, aquatic share,
volcanism, climate normals, and the stable selection of four adjacent founding
pairs.

Generation preserves the v1 cylindrical topology: `x` wraps and `y` is bounded.
The generator may repair at most the eight selected founding tiles, records each
repair, and then revalidates depth, latitude, temperature, volcanism, and daily
light. Hydrogen and sulfur starts are complementary neighboring environments;
the sulfur resource reactions and gas transport arrive in later vertical slices.

The separate `primordial-earth` package remains the frozen one-tile Stage-A
fixture. Keeping it independent prevents procedural-world work from changing
the established world-rule and state-hash golden vectors.
