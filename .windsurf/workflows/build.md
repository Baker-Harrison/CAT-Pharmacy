---
description: Feature development workflow for CAT Pharmacy project
---

# Feature Build Workflow

This workflow handles developing new features for the CAT Pharmacy project, including building the code, testing, committing changes, and updating documentation.

## Steps

1. **Read the documentation**
   - Review `docs/architecture.md` for architectural guidelines
   - Check `AGENTS.md` for coding conventions and project rules
   - Understand the feature requirements and context

2. **Restore NuGet packages**

   ```bash
   dotnet restore src/CatAdaptive.sln
   ```

3. **Build the solution**

   ```bash
   dotnet build src/CatAdaptive.sln --configuration Release
   ```

4. **Run tests (if available)**

   ```bash
   dotnet test src/CatAdaptive.sln --configuration Release --no-build
   ```

5. **Stage and commit changes to git**

   ```bash
   git add .
   git commit -m "feat: [prompt description]"
   ```

6. **Update documentation**
   - Update `docs` if architectural changes were made
   - Update relevant API documentation in code comments
   - Update this workflow file if process changes

## Usage

Use this workflow when you are:
- Building a new feature from a prompt/requirement
- Implementing requested functionality
- Fixing bugs
- Making significant changes to the codebase
- Updating project dependencies

## Notes

- Always run tests before committing if they exist
- Ensure the build succeeds before committing
- Keep commit messages clear and descriptive
- Update documentation only when behavior changes
