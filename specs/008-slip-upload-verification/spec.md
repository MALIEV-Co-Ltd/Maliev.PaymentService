# Feature Specification: Bank Transfer Slip Upload & LLM Verification

**Feature Branch**: `008-slip-upload-verification`  
**Created**: 2026-02-21  
**Status**: Draft  
**Input**: Bank transfer slip upload endpoint with automatic LLM verification for Thai B2B customers

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Customer Uploads Bank Transfer Slip (Priority: P1)

As a Thai B2B customer who has completed a bank transfer, I want to upload a screenshot of my transfer slip so that my payment can be verified and my order processed without manual intervention.

**Why this priority**: This is the core value proposition - enabling customers to self-service their bank transfer payments, reducing support workload and accelerating order fulfillment.

**Independent Test**: Can be fully tested by creating a payment in Pending/Processing status, uploading a valid slip image, and verifying the system accepts it and routes it appropriately (auto-verification or manual review queue).

**Acceptance Scenarios**:

1. **Given** a customer has a payment in Pending status, **When** they upload a valid bank transfer slip image, **Then** the system stores the slip and initiates verification
2. **Given** a customer uploads a slip with matching or exceeding amount, **When** the LLM verification succeeds, **Then** the payment status transitions to Completed automatically
3. **Given** a customer uploads a slip with insufficient amount or invalid image, **When** the LLM verification fails or cannot verify, **Then** the payment status transitions to PendingVerification for manual review

---

### User Story 2 - Payment Auto-Verified by LLM (Priority: P1)

As the payment system, I want to automatically verify valid bank transfer slips using an AI vision model so that customers receive instant payment confirmation without human intervention.

**Why this priority**: Automation of verification directly enables the P1 user story and provides immediate value by reducing manual review workload.

**Independent Test**: Can be fully tested by calling the slip analysis service with various slip images (valid, invalid, non-slip images) and verifying correct extraction of amount, bank name, and validity determination.

**Acceptance Scenarios**:

1. **Given** a clear bank transfer slip image is provided, **When** the LLM analyzes it, **Then** the system extracts: validity flag, amount in THB, bank name, and transfer date
2. **Given** the extracted amount matches or exceeds the payment amount, **When** the slip is valid, **Then** the payment is marked as Completed
3. **Given** a non-slip image is uploaded, **When** the LLM analyzes it, **Then** the system returns isValid=false with explanatory notes

---

### User Story 3 - Manual Review Fallback (Priority: P2)

As a payments operations team member, I want payments that cannot be auto-verified to enter a PendingVerification state so that I can manually review and process them.

**Why this priority**: This is the safety net that ensures no payment is lost due to verification failures, but it depends on P1 functionality being in place.

**Independent Test**: Can be fully tested by triggering various failure scenarios (LLM service unavailable, ambiguous slip, amount mismatch) and verifying payments enter PendingVerification status with the slip URL stored.

**Acceptance Scenarios**:

1. **Given** the LLM verification service is unavailable, **When** a slip is uploaded, **Then** the system gracefully degrades to PendingVerification status without failing the upload
2. **Given** the LLM returns isValid=false, **When** a slip is uploaded, **Then** the payment enters PendingVerification status with the slip stored for manual review
3. **Given** the extracted amount is less than the payment amount, **When** verification completes, **Then** the payment enters PendingVerification status

---

### User Story 4 - Employee Uploads Slip on Behalf of Customer (Priority: P3)

As a customer service employee, I want to upload a slip on behalf of a customer so that I can assist customers who have difficulty with the self-service upload.

**Why this priority**: Enables customer support scenarios but requires both P1 and P2 to be functional first.

**Independent Test**: Can be fully tested by authenticating as an employee with the required permission and uploading a slip for any payment (not owned by the employee).

**Acceptance Scenarios**:

1. **Given** an authenticated employee with payments.slip.upload permission, **When** they upload a slip for any payment, **Then** the upload succeeds regardless of payment ownership
2. **Given** an authenticated customer without employee permissions, **When** they attempt to upload a slip for another customer's payment, **Then** the request is denied with authorization error

---

### Edge Cases

- What happens when a slip is uploaded for a payment already in Completed or Failed status?
- How does the system handle file types other than JPEG, PNG, or WebP?
- What happens when the uploaded file exceeds 10 MB?
- How does the system respond when the upload storage service is unavailable?
- Re-uploading a slip replaces the existing slip URL and re-triggers LLM verification from scratch
- Concurrent slip uploads for the same payment follow last-write-wins semantics; the final completed upload's result is persisted

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow authenticated users to upload bank transfer slip images (JPEG, PNG, WebP) for payments in Pending or Processing status
- **FR-002**: System MUST validate file size does not exceed 10 MB and reject unsupported file types with a 400 error
- **FR-003**: System MUST reject slip uploads for payments not in Pending or Processing status with a 409 Conflict response
- **FR-004**: System MUST store uploaded slips in cloud storage and record the storage URL with the payment transaction
- **FR-005**: System MUST call an LLM-based verification service to analyze the uploaded slip image
- **FR-006**: System MUST automatically mark payments as Completed when the LLM confirms: (a) the image is a valid transfer slip AND (b) the extracted amount is greater than or equal to the payment amount
- **FR-007**: System MUST mark payments as PendingVerification when the LLM verification fails, is inconclusive, or the amount is insufficient
- **FR-008**: System MUST gracefully handle LLM verification service failures by falling back to PendingVerification status without failing the upload request
- **FR-009**: System MUST publish a payment completion event when a payment transitions to Completed status via slip verification
- **FR-010**: System MUST allow employees with appropriate permissions to upload slips for any payment, bypassing ownership checks
- **FR-011**: System MUST only allow customers to upload slips for their own payments (ownership validation)
- **FR-012**: System MUST return a 502 Bad Gateway error when the file storage service is unavailable
- **FR-013**: System MUST allow slip re-uploads for payments in Pending, Processing, or PendingVerification status, replacing the previous slip and re-triggering verification
- **FR-014**: System MUST handle concurrent slip uploads for the same payment using last-write-wins semantics; no locking or conflict errors are returned to users
- **FR-015**: System MUST persist all extracted verification data (extracted amount, bank name, transfer date, notes) with the payment record for audit trail and manual review support

### Key Entities

- **PaymentTransaction**: Extended to include slip-related fields: SlipUrl (storage URL), SlipExtractedAmount (amount extracted by LLM), SlipBankName (bank name from slip), SlipTransferDate (date from slip), SlipVerificationNotes (LLM notes), and SlipVerifiedAt (timestamp of verification). Tracks the payment lifecycle including the new PendingVerification status.
- **PaymentStatus (Enum)**: Extended with a new value PendingVerification (6) to represent payments awaiting manual review of uploaded slips.
- **SlipUploadResponse**: Contains transaction ID, final status, slip URL, auto-verification flag, extracted amount, and user-facing message.
- **SlipAnalysisResult**: Contains validity flag, extracted amount in THB, bank name, transfer date, and notes from the LLM analysis.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Customers can complete a slip upload in under 30 seconds from selection to confirmation
- **SC-002**: Auto-verification completes within 10 seconds of slip upload in 95% of cases
- **SC-003**: Valid slips with matching amounts are auto-verified with 99% accuracy (measured against manual review results)
- **SC-004**: System handles 100 concurrent slip uploads without degradation
- **SC-005**: Zero payment data is lost when the LLM verification service is unavailable (graceful degradation)
- **SC-006**: Manual review workload is reduced by at least 60% for bank transfer payments (measured by comparing PendingVerification rate before and after optimization)
- **SC-007**: 100% of slip upload failures return appropriate HTTP status codes with clear error messages

## Clarifications

### Session 2026-02-22

- Q: What happens if a payment already has a slip uploaded? → A: Replace existing slip and re-trigger LLM verification
- Q: How does the system handle concurrent slip uploads for the same payment? → A: Last write wins - accept final upload result
- Q: Should extracted verification data (bank name, transfer date, extracted amount) be persisted? → A: Store extracted verification data with payment record for audit trail and manual review support

## Assumptions

- The ChatbotService already has or will have a vision analysis endpoint capable of processing slip images
- The upload service already exists and can handle file uploads with configurable paths
- All amounts are in Thai Baht (THB) for slip verification purposes
- The existing PaymentCompletedEvent message contract already supports the Amount field
- Cloud storage URLs are accessible to the LLM verification service
- Authentication and authorization infrastructure is already in place (employee roles, permissions)
