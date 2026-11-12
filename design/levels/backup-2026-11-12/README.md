# Level rules backup (2026-11-12)

Backup taken before the stage 6/16/38 edits that make ★3 (all brothers used) reachable. User decision 2026-11-12 08:00.

## Contents
File names are the repo path with `/` replaced by `_`.

- `current/`: state at 7lucky `c11f86b`, git tag `backup/levels-pre-allbrothers-2026-11-12`.
  - Stage data: `levels.js`, `levels.json`, `chapters.json` (design and Unity copies).
  - Star rule: `core.js`, `Stars.cs`, `stars.md`, `stars.golden.json`. This is the all-brothers ★3 rule from 3f3a14c.
  - `all-brothers-check.json`: 47/50; 6, 16 and 38 cannot use all brothers.
- `pre-allbrothers/`: the same files at `919b7c6`, the first parent of 3f3a14c.
  - This is the old star rule: ★3 = minBodies, no undo or restart.
  - There is no `all-brothers-check.json` at that commit.

## Restore
Whole tree, from the tag:
```bash
git checkout backup/levels-pre-allbrothers-2026-11-12 -- prototypes/seven-steps/src/levels.js prototypes/seven-steps/src/core.js design/levels/stars.md design/levels/chapters.json unity/Assets/_Project/Data/levels.json unity/Assets/_Project/Data/chapters.json unity/Assets/_Project/Scripts/Runtime/Core/Stars.cs
```

Old star rule: use `919b7c6` instead of the tag. The star rule also touches telemetry, the server and qa-play; see `git show 3f3a14c --stat`. Revert that merge to restore it fully: `git revert -m 1 3f3a14c`.

After a restore, run:
```bash
unity/Tools/unity.sh levels
unity/Tools/unity.sh golden
unity/Tools/unity.sh verify
unity/Tools/unity.sh test
```

The copies here are for reading and diffing only. Restore from git, not by copying these files back.
