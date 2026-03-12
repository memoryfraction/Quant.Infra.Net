-- =============================================
-- Orders schema patch for payment flow
-- Safe to run multiple times on PostgreSQL / NeonDB
-- =============================================

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

CREATE TABLE IF NOT EXISTS public."Orders" (
    "Id" UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    "UserId" UUID NOT NULL REFERENCES public."Users"("Id") ON DELETE RESTRICT,
    "ProductId" UUID NOT NULL REFERENCES public."Products"("Id") ON DELETE RESTRICT,
    "PriceId" UUID NOT NULL REFERENCES public."Prices"("Id") ON DELETE RESTRICT,
    "SubscriptionId" UUID NULL,
    "OriginalAmount" BIGINT NOT NULL,
    "ActualAmount" BIGINT NOT NULL,
    "DiscountAmount" BIGINT NOT NULL DEFAULT 0,
    "Status" SMALLINT NOT NULL DEFAULT 0,
    "ExpiredTime" TIMESTAMPTZ NULL,
    "PaidTime" TIMESTAMPTZ NULL,
    "Metadata" JSONB NULL,
    "CreatedTime" TIMESTAMPTZ NOT NULL DEFAULT timezone('utc', now()),
    "IsDeleted" BOOLEAN NOT NULL DEFAULT false
);

ALTER TABLE public."Subscriptions"
    ADD COLUMN IF NOT EXISTS "OrderId" UUID NULL;

ALTER TABLE public."Transactions"
    ADD COLUMN IF NOT EXISTS "OrderId" UUID NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'FK_Orders_Subscriptions_SubscriptionId'
    ) THEN
        ALTER TABLE public."Orders"
            ADD CONSTRAINT "FK_Orders_Subscriptions_SubscriptionId"
            FOREIGN KEY ("SubscriptionId") REFERENCES public."Subscriptions"("Id") ON DELETE SET NULL;
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'FK_Subscriptions_Orders_OrderId'
    ) THEN
        ALTER TABLE public."Subscriptions"
            ADD CONSTRAINT "FK_Subscriptions_Orders_OrderId"
            FOREIGN KEY ("OrderId") REFERENCES public."Orders"("Id") ON DELETE RESTRICT;
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'FK_Transactions_Orders_OrderId'
    ) THEN
        ALTER TABLE public."Transactions"
            ADD CONSTRAINT "FK_Transactions_Orders_OrderId"
            FOREIGN KEY ("OrderId") REFERENCES public."Orders"("Id") ON DELETE RESTRICT;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS "IX_Orders_UserId_Status" ON public."Orders"("UserId", "Status");
CREATE INDEX IF NOT EXISTS "IX_Transactions_OrderId_Status" ON public."Transactions"("OrderId", "Status");
