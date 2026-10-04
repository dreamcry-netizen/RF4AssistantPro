# RF4 Assistant Pro — Dark Natural UI

This source keeps the existing v1.9.87 application logic and applies the selected dark natural/forest visual direction.

## Changed
- Dark graphite/forest base palette.
- Olive/lime accent instead of bright blue.
- Darker cards, borders, navigation and status elements.
- Main window default size adjusted for the denser dashboard layout.

## Intentionally unchanged
- OCR and parsers.
- Global keyboard hooks.
- Capture logic.
- Storage, backup/restore and transactions.
- Statistics and view-model logic.
- Existing button names, event handlers and bindings.

## Build
Build and test on Windows with the .NET 10 SDK using the existing BUILD.md / QA instructions.


## Dark Natural — pass 2
- Unified global WPF styles for DataGrid, tabs and checkboxes.
- Updated bait catalog to the graphite/olive palette.
- Updated programmatic OCR/cafe/storage review windows to the same dark-natural base.
- Business logic, OCR parsing, storage, hooks and capture were not intentionally changed.
