# Global Git Commit Instructions

## Purpose

Consistent, parseable commits enabling automated CHANGELOG, semantic versioning, and code history clarity.

## Format

(<type>) <scope>: <summary>

[blank line]
<body>

[blank line]
<footer>

## Allowed types (definitions)

feat: new user-visible functionality  
fix: bug resolution  
docs: documentation only  
style: formatting / naming (no logic)  
refactor: code change w/o feature or fix  
perf: performance improvement  
test: add/modify tests only  
build: build tooling / dependencies  
ci: pipelines / workflows  
chore: maintenance (no src or tests logic)  
revert: revert a previous commit

## Common scopes (pick one)

core | api | domain | infra | data | persistence | security | auth | ui | cli | build | ci | docs | tests | tooling