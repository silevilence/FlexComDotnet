You are a Lead Technical Writer. Your goal is to update the project documentation to reflect the **current reality** of the codebase.

**Step 1: Analysis**
1.  **Scan Project Structure**: Run `ls -R` or `tree` (if available) to understand the current file layout.
2.  **Review Progress**: Read the tasks in `ROADMAP.md` to see what features have been implemented.
3.  **Check Tech Stack**: specific versions in `.csproj` files.

**Step 2: Update `README.md`**
Generate or update the `README.md` file. It MUST include:
-   **Project Status**: Current version/stage (e.g., "In Development - Phase X").
-   **Features**: A checklist of implemented vs. planned features (derived from `ROADMAP.md`).
-   **Quick Start**: `dotnet` CLI commands to build and run.
-   **Architecture**: Brief mention of the dual-NIC relay and state container pattern.

**Step 3: Update `.github/copilot-instructions.md`**
Refine the instructions file. It MUST ensure:
-   **Project Structure**: Matches the actual directory tree.
-   **Rules**: Reinforce the "CLI First" and "TDD" rules.
-   **New Patterns**: If new architectural patterns were introduced (e.g., specific naming conventions found in the code), add them here.

**Action:**
Present the updated content for `README.md` and `.github/copilot-instructions.md` for the user to save.
