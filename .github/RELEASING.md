# SDK snapshot releases

New SDK snapshot tags use `<game-version>-<weekly-label>`, for example `0.2.10-26w44a`. The game version is read from `VERSION_NUMBER` in the selected Redux source commit. Its first three numeric components form the package's SemVer base. Do not prepend a fixed `0.2.9` value or derive a game version from the date.

Use the Redux game's `scripts/new-sdk-snapshot.ps1` and `.claude/skills/sdk-snapshot/SKILL.md`. They resolve the selected source branch to a commit once, read that commit's game version and SDK gitlink, and prepare Redux.Templates with the same full version, registry dependency and SDK source provenance.

The weekly label is optional. When omitted, the helper uses the current UTC ISO week and advances past the highest suffix in either repository, including both old bare labels and full version tags. An explicitly supplied label is honored. Keep the captured label and source commit throughout the release.

The SDK tag remains annotated and points to the exact SDK commit pinned by the game. The registry publisher takes the version from that tag and writes it into the published archive's `package.json`; it preserves the pinned source files and records their original manifest version. No version-only commit or game gitlink change is required to label a release.

Push the SDK tag first and verify that the public registry version reports the pinned `gitHead`. Only then publish the matching template commit and lightweight tag. Templates use `https://upm.ksp2redux.org`, package `ksp2redux.sdk`, and the exact full version. The SDK Manager understands both these full versions and older bare weekly tags.

Examples of successive release series are `0.2.9-26w43a`, `0.2.10-26w44a`, and `0.3.0-27w01a`. These are examples, not configured defaults. Existing bare weekly tags keep their recorded registry versions; snapshots before the package rename use the historical `0.10.0` series.

Do not move old tags or overwrite published archives. Team members can sign in through Authentik for manual package publication and management, while normal SDK snapshots continue publishing automatically from tags. See the [registry guide](https://docs.ksp2redux.org/s/0a4a3e31-0b40-48e0-bd81-e14856f80d3d/doc/install-and-update-the-sdk-from-the-unity-registry-OdYzNhX0dW).
