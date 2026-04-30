# Card Audit Baselines

The root `docs/card-audit/cards.*` files are the current public-build audit used for release notes and review.

Versioned folders here hold additional known-good game branch snapshots. `check-card-audit.ps1` accepts any `cards.csv` in this folder as a valid match, so switching between the current public build and a reviewed public beta does not look like an unaudited card change.

- `v0.104.0-public-beta/`: Slay the Spire 2 public beta `v0.104.0`, exported from the local game install.
