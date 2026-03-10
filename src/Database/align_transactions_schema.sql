-- Align Transactions schema to current PostgreSQL baseline.
-- Safe to run multiple times.

-- 1) Remove legacy columns that are no longer used by code.
ALTER TABLE public."Transactions"
    DROP COLUMN IF EXISTS "Metadata",
    DROP COLUMN IF EXISTS "UpdatedTime";

-- 2) Ensure OrderId is required (only enforce NOT NULL when data is clean).
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'Transactions'
          AND column_name = 'OrderId'
          AND is_nullable = 'YES'
    ) THEN
        IF EXISTS (
            SELECT 1
            FROM public."Transactions"
            WHERE "OrderId" IS NULL
        ) THEN
            RAISE NOTICE 'Skip setting Transactions.OrderId NOT NULL because null rows exist.';
        ELSE
            ALTER TABLE public."Transactions"
                ALTER COLUMN "OrderId" SET NOT NULL;
        END IF;
    END IF;
END $$;

-- 3) Keep only the baseline index required by current schema.
CREATE INDEX IF NOT EXISTS "IX_Transactions_OrderId_Status"
    ON public."Transactions" ("OrderId", "Status");

DROP INDEX IF EXISTS public."IX_Transactions_UserId";
DROP INDEX IF EXISTS public."IX_Transactions_SubscriptionId";
DROP INDEX IF EXISTS public."IX_Transactions_Status";
DROP INDEX IF EXISTS public."IX_Transactions_ExternalTransactionId";
DROP INDEX IF EXISTS public."IX_Transactions_CreatedTime";
