-- =============================================
-- Orders table for Saas.Infra.Net
-- PostgreSQL / NeonDB
-- =============================================

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ------------------------------
-- Core business table: Orders
-- Unified entry point for payment flow
-- ------------------------------
CREATE TABLE IF NOT EXISTS public."Orders" (
    "Id" UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    "UserId" UUID NOT NULL REFERENCES public."Users"("Id") ON DELETE RESTRICT,
    "ProductId" UUID NOT NULL REFERENCES public."Products"("Id") ON DELETE RESTRICT,
    "PriceId" UUID NOT NULL REFERENCES public."Prices"("Id") ON DELETE RESTRICT,
    "SubscriptionId" UUID NULL,
    "OriginalAmount" BIGINT NOT NULL, -- original price snapshot, in cents
    "ActualAmount" BIGINT NOT NULL, -- actual payable amount, in cents
    "DiscountAmount" BIGINT NOT NULL DEFAULT 0, -- discount amount, in cents
    "Status" SMALLINT NOT NULL DEFAULT 0, -- 0=Pending, 1=Paid, 2=Cancelled, 3=Refunded
    "ExpiredTime" TIMESTAMPTZ NULL, -- expires if not paid in time
    "PaidTime" TIMESTAMPTZ NULL, -- actual payment success time
    "Metadata" JSONB NULL, -- reserved for product-specific data
    "CreatedTime" TIMESTAMPTZ NOT NULL DEFAULT timezone('utc', now()),
    "IsDeleted" BOOLEAN NOT NULL DEFAULT false
);

-- ------------------------------
-- Optional FK: link to subscription after payment succeeds
-- ------------------------------
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = 'public'
          AND table_name = 'Subscriptions'
    ) AND NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'FK_Orders_Subscriptions_SubscriptionId'
    ) THEN
        ALTER TABLE public."Orders"
            ADD CONSTRAINT "FK_Orders_Subscriptions_SubscriptionId"
            FOREIGN KEY ("SubscriptionId") REFERENCES public."Subscriptions"("Id") ON DELETE SET NULL;
    END IF;
END $$;

-- ------------------------------
-- Indexes
-- ------------------------------
CREATE INDEX IF NOT EXISTS "IX_Orders_UserId_Status"
    ON public."Orders"("UserId", "Status");

CREATE INDEX IF NOT EXISTS "IX_Orders_ProductId"
    ON public."Orders"("ProductId");

CREATE INDEX IF NOT EXISTS "IX_Orders_PriceId"
    ON public."Orders"("PriceId");

CREATE INDEX IF NOT EXISTS "IX_Orders_ExpiredTime"
    ON public."Orders"("ExpiredTime");
