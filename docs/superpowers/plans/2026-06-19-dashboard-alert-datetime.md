# Dashboard and Alert Date-Time Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:test-driven-development.

**Goal:** Display Dashboard and Alert timestamps as `dd/MM/yyyy HH:mm:ss`.

**Architecture:** Update the shared formatter, normalize both API and demo service results, and store demo timestamps as parseable ISO values.

**Tech Stack:** TypeScript, Vitest.

---

- [ ] Add a failing formatter test.
- [ ] Update the shared formatter.
- [ ] Normalize alert and operation-event fallbacks.
- [ ] Replace relative demo labels with ISO timestamps.
- [ ] Run the focused test.
