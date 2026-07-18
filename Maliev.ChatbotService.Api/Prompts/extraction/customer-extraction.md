---
name: "Customer Data Extraction"
category: Topic
topic_key: "customer-extraction"
priority: 0
is_active: true
---

# Customer Data Extraction Instructions

Extract customer information from the following data. The input may be in Thai, English, or mixed.
Return all fields you can identify. Extract values as-is in their original language.

## Fields to Extract

- **first_name**, **last_name**: person's name (ชื่อ, นามสกุล)
- **email**: email address (อีเมล)
- **mobile**: mobile/cell phone number (มือถือ, โทรศัพท์)
- **landline**: Customer's PERSONAL landline number (NOT company phone). Only use if it clearly belongs to the individual contact, not the organization.
- **extension**: Phone extension number for reaching the contact through company phone. Extract ONLY the numeric digits (strip "#", "ext.", "ต่อ", etc.).
- **company_phone**: Company main phone number (บริษัท phone, สำนักงาน). If a phone number appears with extension notation (e.g., "#", "ext.", "ต่อ"), this is the company phone.
- **segment**: customer segment if mentioned (must be one of: Retail, Wholesale, Enterprise, Government)
- **company_name**: company/business name (ชื่อบริษัท, บริษัท, หจก., ห้างหุ้นส่วน). Look for "บริษัท", "Co., Ltd.", "จำกัด", etc.
- **vat_number**: tax ID / VAT number / เลขประจำตัวผู้เสียภาษี (typically 13 digits for Thai companies). Extract ONLY the numeric digits.
- **branch_number**: branch number (สาขาที่). When the tax ID line contains "สาขาที่" or "Branch" followed by a number, extract the value EXACTLY as written preserving leading zeros (e.g., "สาขาที่ 00012" → branch_number = "00012", NOT "12"). If the text says "สำนักงานใหญ่" (head office), use "00000". If no branch or head office is mentioned, leave empty.

## Address Parsing Rules

For addresses, parse into structured fields. IMPORTANT: You MUST extract ALL fields.

- **address_line_1** (CRITICAL — DO NOT SKIP): This is the street-level address. It includes EVERYTHING before the sub-district: house number (เลขที่), moo/หมู่ (ม.), village/หมู่บ้าน (มบ./หมู่บ้าน), soi/ซอย, road/ถนน. This field should NEVER be empty if the input contains address details.
- **address_line_2**: Building name, floor, unit number (if available). DO NOT include "Head Office", "สำนักงานใหญ่", or branch information here.
- **address_line_3**: Any additional address info. DO NOT include "Head Office", "สำนักงานใหญ่", or branch information here.
- **district**: ONLY the sub-district name (ตำบล/แขวง/tambon). Strip prefix like ต. or ตำบล. e.g. "บางมด" or "ทุ่งสองห้อง".
- **city**: ONLY the district name (อำเภอ/เขต/amphoe). Strip prefix like อ. or อำเภอ. e.g. "จอมทอง" or "หลักสี่".
- **state_province**: ONLY the province name (จังหวัด). Strip prefix like จ. or จังหวัด. e.g. "กรุงเทพมหานคร" or "สมุทรปราการ".
- **postal_code**: the 5-digit postal code. IMPORTANT: Copy it EXACTLY as written.


## Phone Number Extraction Priority

When you encounter a phone number with an extension (e.g., "021234567 #888", "02-123-4567 ext. 999", "021234567 ต่อ 777"):
1. Extract the main number → **company_phone**: "021234567"
2. Extract the extension digits only → **extension**: "888" (strip "#", "ext.", "ต่อ")
3. Leave **landline** empty (it's a company phone, not personal)

**Example 1: Phone with extension**
```
Input: "021234567 #888"
Output:
  company_phone: "021234567"
  extension: "888"
  landline: null
```

**Example 2: Personal landline**
```
Input: "02-999-8888 (home)"
Output:
  landline: "029998888"
  company_phone: null
  extension: null
```

**Example 3: Mobile phone**
```
Input: "0891112222"
Output:
  mobile: "0891112222"
  company_phone: null
  landline: null
  extension: null
```


## Shipping Address Recipient

When a shipping address includes a person's name and/or phone number, extract them:
- **recipient_name**: the full name of the shipping recipient (ชื่อผู้รับ). This is often the first line after the "ส่ง" / "จัดส่ง" keyword.
- **recipient_phone**: the phone number associated with the shipping address recipient.

These fields are specific to shipping addresses. Billing addresses typically do not have separate recipient info.

### Parsing Example 1

Given this input:
```
สมชาย ดีมาก
99/99 ม.9 มบ.รวยรวย
ต.บางมด
อ. จอมทอง จ. กรุงเทพ
10150
```

Extract as:
- first_name: "สมชาย"
- last_name: "ดีมาก"
- address_line_1: "99/99 ม.9 มบ.รวยรวย"
- district: "บางมด"
- city: "จอมทอง"
- state_province: "กรุงเทพ"
- postal_code: "10150"

### Parsing Example 2

Given this input:
```
กมลรัตน์ รักดี
บริษัท เอ็นเตอร์ไพรส์ไทย จำกัด
เลขประจำตัวผู้เสียภาษี 0105561001234 (สาขาที่ 00005)
77/7 หมู่ 7
ตำบลสวนผัก ตลิ่งชัน
กรุงเทพ 10170
0891234567
kamolrat.r@enterprise.co.th

ส่ง
วรรณนา ชูใจ
55/5 หมู่ 5
คลองตัน วัฒนา
กรุงเทพ 10110
0819876543
```

Extract as:
- first_name: "กมลรัตน์"
- last_name: "รักดี"
- email: "kamolrat.r@enterprise.co.th"
- mobile: "0891234567"
- company_name: "บริษัท เอ็นเตอร์ไพรส์ไทย จำกัด"
- vat_number: "0105561001234"
- branch_number: "00005"
- Billing address:
  - address_line_1: "77/7 หมู่ 7"
  - district: "สวนผัก" (extract as-is even if misspelled)
  - city: "ตลิ่งชัน"
  - state_province: "กรุงเทพ"
  - postal_code: "10170"
- Shipping address:
  - recipient_name: "วรรณนา ชูใจ"
  - recipient_phone: "0819876543"
  - address_line_1: "55/5 หมู่ 5"
  - district: "คลองตัน"
  - city: "วัฒนา"
  - state_province: "กรุงเทพ"
  - postal_code: "10110"


## Address Type Detection

For Thai addresses, detect address type using these keywords:

- "วางบิล" or "ที่อยู่ออกบิล" or no keyword = **Billing** (default)
- "ส่ง" or "จัดส่ง" or "ที่อยู่จัดส่ง" = **Shipping**

If two addresses are present, the first is Billing and the second is Shipping unless keywords indicate otherwise.
Return each address as a separate object in the addresses array.

## Important Rules

1. **address_line_1 is MANDATORY**: Any line containing a house number (digits followed by / or ม. or หมู่ or ซ. or ถ.) MUST be extracted as address_line_1. A line like "11/1 หมู่ 1" is address_line_1, NOT part of the district. If you see a number/number pattern (e.g., 11/1, 22/22, 33/33), that entire line is address_line_1.
2. **Postal code is EXACT**: Copy the 5-digit postal code exactly as written. Do not correct or infer postal codes.
3. **Branch number preserves zeros**: Extract branch_number exactly as written, including leading zeros (e.g., "00012" not "12").
4. **Shipping recipient**: When text after "ส่ง"/"จัดส่ง" starts with a person's name, that's the recipient_name. A phone number near the shipping address is the recipient_phone.

