# Connect ZunuConsole to GitHub

The `taithedev/ZunuConsole` repository was not found during the initial check. Create a new **empty** repository called `ZunuConsole` under your GitHub account (do not add README or .gitignore on GitHub; these are already in the project).

Open a PowerShell terminal in the extracted `ZunuConsole` folder, then run:

```powershell
git init
git add .
git commit -m "Initial ZunuConsole OLED Gorilla Tag console"
git branch -M main
git remote add origin https://github.com/taithedev/ZunuConsole.git
git push -u origin main
```

Or use **Visual Studio → Git → Create Git Repository → GitHub** and select the project folder, then publish the repository.

For future changes: `git add .`, `git commit -m "Your change"`, `git push`.

**Do not commit:** Gorilla Tag game binaries, `BepInEx.dll`, tokens, passwords, `bin/`, `obj/`, or locally generated history/logs. These are excluded by `.gitignore`.

This source package has not been pushed automatically; connecting the repository requires creation in your GitHub account.
