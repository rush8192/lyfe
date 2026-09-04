# Primordial Earth world pack

This directory contains the first complete, closed-schema world profile. The
foundation profile is intentionally one volcanic ocean tile: it proves independent
world-package selection and typed resource initialization before the procedural
primordial-Earth generator is implemented.

The package cannot introduce executable code. It references resources owned by the
compatible biological pack and uses the v1 cylindrical topology: east-west wraps,
north-south does not.

`requiredRegistryManifestHash` pins the meanings of every resource key and numeric
ID. Compilation resolves the profile's stocks to the selected rule pack's dense
resource manifest and rejects mismatched registries before world creation.
