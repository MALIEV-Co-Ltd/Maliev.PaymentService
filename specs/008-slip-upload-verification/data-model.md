# Data Model: Bank Transfer Slip Upload & LLM Verification

**Feature**: 008-slip-upload-verification
**Date**: 2026-02-22

## Entity Extensions

### PaymentTransaction

**Existing Entity**: Extended to support slip upload and verification data.

#### New Properties

| Property | Type | Nullable | Max Length | Description |
|----------|------|----------|------------|-------------|
| SlipUrl | string | Yes | 2000 | GCS URL of uploaded slip image |
| SlipExtractedAmount | decimal? | Yes | (18,2) | Amount extracted by LLM in THB |
| SlipBankName | string | Yes | 100 | Bank name extracted from slip |
| SlipTransferDate | string | Yes | 20 | Transfer date from slip (ISO 8601) |
| SlipVerificationNotes | string | Yes | 500 | Notes from LLM verification |
| SlipVerifiedAt | DateTime? | Yes | - | Timestamp when verification completed |

#### Property Constraints

- All slip properties nullable (payment may not have slip)
- SlipUrl populated on successful upload
- Other slip properties populated on successful LLM response
- All properties overwritten on re-upload

---

### PaymentStatus (Enum Extension)

**Existing Enum**: Extended with new status value.

#### New Value

| Value | Name | Numeric | Description |
|-------|------|---------|-------------|
| 6 | PendingVerification | 6 | Slip uploaded, awaiting LLM or manual verification |

#### Complete Status List (Post-Change)

| Value | Name | Numeric | Description |
|-------|------|---------|-------------|
| Pending | 0 | Payment created, not sent to provider |
| Processing | 1 | Being processed by provider |
| Completed | 2 | Successfully completed |
| Failed | 3 | Failed during processing |
| Refunded | 4 | Fully refunded |
| PartiallyRefunded | 5 | Partially refunded |
| **PendingVerification** | **6** | **Slip uploaded, awaiting verification** |

---

## State Transitions

### Slip Upload Flow

```
                    ┌─────────────┐
                    │   Pending   │
                    │      0      │
                    └──────┬──────┘
                           │
                    Upload Slip
                           │
                           ▼
              ┌────────────────────────┐
              │  LLM Verification      │
              └───────────┬────────────┘
                          │
           ┌──────────────┼──────────────┐
           │              │              │
           ▼              ▼              ▼
    ┌────────────┐ ┌────────────┐ ┌────────────┐
    │   Valid    │ │  Invalid   │ │  Service   │
    │ Amount OK  │ │  or Low    │ │ Unavailable│
    └─────┬──────┘ └─────┬──────┘ └─────┬──────┘
          │              │              │
          ▼              ▼              ▼
    ┌──────────┐  ┌──────────────────────────┐
    │Completed │  │   PendingVerification    │
    │    2     │  │           6              │
    └──────────┘  └──────────────────────────┘
                                          │
                                   Manual Review
                                          │
                           ┌──────────────┴──────────────┐
                           │                             │
                           ▼                             ▼
                    ┌──────────┐                  ┌──────────┐
                    │Completed │                  │  Failed  │
                    │    2     │                  │    3     │
                    └──────────┘                  └──────────┘
```

### Allowed Status Transitions for Slip Upload

| Current Status | Slip Upload Allowed? | Notes |
|----------------|----------------------|-------|
| Pending (0) | YES | Primary use case |
| Processing (1) | YES | Payment in progress |
| PendingVerification (6) | YES | Re-upload allowed (FR-013) |
| Completed (2) | NO | 409 Conflict |
| Failed (3) | NO | 409 Conflict |
| Refunded (4) | NO | 409 Conflict |
| PartiallyRefunded (5) | NO | 409 Conflict |

---

## New DTOs

### SlipAnalysisResult

**Purpose**: Response from ChatbotService LLM analysis.

| Property | Type | Nullable | Description |
|----------|------|----------|-------------|
| IsValid | bool | No | True if valid transfer slip |
| ExtractedAmountThb | decimal? | Yes | Amount in THB, null if unreadable |
| BankName | string | Yes | Bank name from slip |
| TransferDate | string | Yes | ISO 8601 date (yyyy-MM-dd) |
| Notes | string | No | Empty or explanation if invalid |

---

### SlipUploadResponse

**Purpose**: Response to slip upload API call.

| Property | Type | Nullable | Description |
|----------|------|----------|-------------|
| TransactionId | Guid | No | Payment transaction ID |
| Status | string | No | Final status name |
| SlipUrl | string | No | Storage URL of uploaded slip |
| AutoVerified | bool | No | True if auto-verified by LLM |
| ExtractedAmountThb | decimal? | Yes | Amount extracted by LLM |
| Message | string | No | User-facing status message |

#### Message Values

| Scenario | Message |
|----------|---------|
| Auto-verified (Completed) | "Payment verified automatically." |
| Pending review | "Slip uploaded. Pending manual review." |

---

## Database Migration

### AddSlipUrlToPaymentTransaction

**Migration Type**: AddColumn (non-breaking)

#### SQL (PostgreSQL)

```sql
ALTER TABLE payment_transactions ADD COLUMN slip_url VARCHAR(2000) NULL;
ALTER TABLE payment_transactions ADD COLUMN slip_extracted_amount DECIMAL(18,2) NULL;
ALTER TABLE payment_transactions ADD COLUMN slip_bank_name VARCHAR(100) NULL;
ALTER TABLE payment_transactions ADD COLUMN slip_transfer_date VARCHAR(20) NULL;
ALTER TABLE payment_transactions ADD COLUMN slip_verification_notes VARCHAR(500) NULL;
ALTER TABLE payment_transactions ADD COLUMN slip_verified_at TIMESTAMP NULL;
```

#### Rollback

```sql
ALTER TABLE payment_transactions DROP COLUMN slip_url;
ALTER TABLE payment_transactions DROP COLUMN slip_extracted_amount;
ALTER TABLE payment_transactions DROP COLUMN slip_bank_name;
ALTER TABLE payment_transactions DROP COLUMN slip_transfer_date;
ALTER TABLE payment_transactions DROP COLUMN slip_verification_notes;
ALTER TABLE payment_transactions DROP COLUMN slip_verified_at;
```

---

## EF Core Configuration

```csharp
// In PaymentTransactionConfiguration.cs

builder.Property(p => p.SlipUrl)
    .HasColumnName("slip_url")
    .HasMaxLength(2000);

builder.Property(p => p.SlipExtractedAmount)
    .HasColumnName("slip_extracted_amount")
    .HasPrecision(18, 2);

builder.Property(p => p.SlipBankName)
    .HasColumnName("slip_bank_name")
    .HasMaxLength(100);

builder.Property(p => p.SlipTransferDate)
    .HasColumnName("slip_transfer_date")
    .HasMaxLength(20);

builder.Property(p => p.SlipVerificationNotes)
    .HasColumnName("slip_verification_notes")
    .HasMaxLength(500);

builder.Property(p => p.SlipVerifiedAt)
    .HasColumnName("slip_verified_at");
```

---

## Index Recommendations

No additional indexes required for slip properties:
- Queries by payment ID already use primary key
- Slip data retrieved with payment (no separate queries)
- If manual review queue queries needed later, consider index on `Status` (already exists)
