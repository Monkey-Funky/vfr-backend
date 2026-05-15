import openpyxl
import json

wb = openpyxl.load_workbook(r'g:\Graduate_Project_Backend\Infrastructure\Data\Data.xlsx', data_only=True)
ws = wb.active

rows = []
headers = []
for i, row in enumerate(ws.iter_rows(values_only=True)):
    if i == 0:
        headers = [str(c).strip() if c else f"Col{j}" for j, c in enumerate(row)]
        print(f"Headers: {headers}")
        continue
    row_dict = {}
    for j, cell in enumerate(row):
        if j < len(headers):
            row_dict[headers[j]] = str(cell).strip() if cell is not None else ""
    # Skip empty rows (no ID)
    if row_dict.get("ID for Image", "").strip():
        rows.append(row_dict)

print(f"\nTotal non-empty data rows: {len(rows)}")
print(f"\n--- First 5 rows ---")
for i, r in enumerate(rows[:5]):
    print(f"Row {i+1}: {json.dumps(r, ensure_ascii=False)}")

print(f"\n--- Rows 98-100+ ---")
for i, r in enumerate(rows[97:102]):
    print(f"Row {98+i}: {json.dumps(r, ensure_ascii=False)}")

print(f"\n--- Image URL examples ---")
for i in range(min(5, len(rows))):
    print(f"  Row {i+1} Image URL: {rows[i].get('Image URL', 'N/A')}")

# Build correct URLs since formula isn't computed
# The formula pattern is: https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/{ID}.jpg
for r in rows:
    img_id = r["ID for Image"]
    r["Image URL"] = f"https://res.cloudinary.com/ddjzbouvr/image/upload/v1777056667/{img_id}.jpg"

print(f"\n--- Corrected Image URL examples ---")
for i in range(min(5, len(rows))):
    print(f"  Row {i+1}: {rows[i]['Image URL']}")

# Analyze unique values
print(f"\n--- Unique Primary Categories ---")
print(sorted(set(r["Primary Category"] for r in rows)))

print(f"\n--- Unique Broad Categories ---")
print(sorted(set(r["Broad Category"] for r in rows)))

print(f"\n--- Unique Sub-Categories ---")
subcats = sorted(set(r["Sub-Category"] for r in rows))
for sc in subcats:
    print(f"  {sc}")

print(f"\n--- Unique Slots ---")
print(sorted(set(r["Slot"] for r in rows)))

print(f"\n--- Unique Suitable Seasons ---")
seasons = sorted(set(r["Suitable Season"] for r in rows))
for s in seasons:
    print(f"  {s}")

# Save corrected data
with open(r'g:\Graduate_Project_Backend\Infrastructure\Data\data.json', 'w', encoding='utf-8') as f:
    json.dump(rows, f, ensure_ascii=False, indent=2)
print(f"\nCorrected data saved to data.json ({len(rows)} items)")
