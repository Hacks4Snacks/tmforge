# Threat Model Analysis Object Model

This project provides the core object model used to inspect threat model (`.tm7`) documents. It exposes the base types that analysis rules build on, so additional rule sets can live in separate assemblies by deriving from those base classes. The `tmforge lint` command runs rules against a model using this library.
