# Contributing to InnerFrame

## First-time setup (once per computer)

1. **Install Git and Git LFS.** Git LFS stores big files (images, models, audio)
   outside the normal git history.
   - macOS: `brew install git git-lfs`
   - Windows: install [Git for Windows](https://git-scm.com/download/win).
     Git LFS is included; keep the "Git LFS" box ticked.
2. **Clone the repo** somewhere that is *not* synced by iCloud or OneDrive
   (on a Mac, not in Desktop or Documents):
   ```
   git clone https://github.com/Vanderbilt-VR-2026/InnerFrame.git
   ```
3. **Run the setup script** from inside the repo folder (on Windows, in Git Bash):
   ```
   bash scripts/setup.sh
   ```

That's all. Running the setup script again is harmless.

## Daily loop

Each of us works on a personal branch named after us. Right now these are
`CiciLuo`, `charlyne_dong`, `serena_deng`, and `sonya_vu`.
`main` is protected: changes reach it only through a pull request that a
teammate approves.

1. **Switch to your branch:** `git switch <your-branch>`
2. **Get the latest work:** `git pull`, then `git pull origin main`
3. **Work in Unity, then commit**, including the `.meta` files Unity creates:
   ```
   git add -A
   git commit -m "Short description of what you did"
   ```
4. **Push:** `git push`. Before anything is uploaded, a quick check runs on
   your commits (missing `.meta` files, big files not in LFS, and so on). If
   it stops the push, it says what to fix. Fix it, commit, and push again.
5. **Open a pull request** on GitHub from your branch into `main`, and ask a
   teammate to review it. The same check runs there automatically.
