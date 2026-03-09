-- =============================================
-- 测试数据脚本（产品、价格、角色、测试用户）
-- Test data script (Products, Prices, Roles, Test User)
-- =============================================

-- 注意：执行此脚本前请确保以下表已存在：
-- - Users
-- - Roles
-- - UserRoles
-- - Products
-- - Prices

-- =============================================
-- 1. 创建角色（如果不存在）
-- =============================================
DO $$
DECLARE
    v_super_admin_role_id UUID;
    v_admin_role_id UUID;
    v_user_role_id UUID;
BEGIN
    -- 检查并创建 SUPER_ADMIN 角色
    SELECT "Id" INTO v_super_admin_role_id FROM public."Roles" WHERE "Code" = 'SUPER_ADMIN' LIMIT 1;

    IF v_super_admin_role_id IS NULL THEN
        INSERT INTO public."Roles" ("Id", "Name", "Code", "Description", "CreatedTime")
        VALUES (
            uuid_generate_v4(),
            '超级管理员',
            'SUPER_ADMIN',
            'Super administrator with full access',
            timezone('utc', now())
        )
        RETURNING "Id" INTO v_super_admin_role_id;

        RAISE NOTICE 'SUPER_ADMIN role created: %', v_super_admin_role_id;
    ELSE
        RAISE NOTICE 'SUPER_ADMIN role already exists: %', v_super_admin_role_id;
    END IF;

    -- 检查并创建 ADMIN 角色
    SELECT "Id" INTO v_admin_role_id FROM public."Roles" WHERE "Code" = 'ADMIN' LIMIT 1;
    
    IF v_admin_role_id IS NULL THEN
        INSERT INTO public."Roles" ("Id", "Name", "Code", "Description", "CreatedTime")
        VALUES (
            uuid_generate_v4(),
            'Administrator',
            'ADMIN',
            'System administrator with full access',
            timezone('utc', now())
        )
        RETURNING "Id" INTO v_admin_role_id;
        
        RAISE NOTICE 'ADMIN role created: %', v_admin_role_id;
    ELSE
        RAISE NOTICE 'ADMIN role already exists: %', v_admin_role_id;
    END IF;
    
    -- 检查并创建 USER 角色
    SELECT "Id" INTO v_user_role_id FROM public."Roles" WHERE "Code" = 'USER' LIMIT 1;
    
    IF v_user_role_id IS NULL THEN
        INSERT INTO public."Roles" ("Id", "Name", "Code", "Description", "CreatedTime")
        VALUES (
            uuid_generate_v4(),
            'User',
            'USER',
            'Regular user with limited access',
            timezone('utc', now())
        )
        RETURNING "Id" INTO v_user_role_id;
        
        RAISE NOTICE 'USER role created: %', v_user_role_id;
    ELSE
        RAISE NOTICE 'USER role already exists: %', v_user_role_id;
    END IF;
END $$;

-- =============================================
-- 1.1 创建测试用户并授予 SUPER_ADMIN（如果不存在）
-- =============================================
DO $$
DECLARE
    v_test_user_id UUID;
    v_super_admin_role_id UUID;
BEGIN
    SELECT "Id" INTO v_super_admin_role_id
    FROM public."Roles"
    WHERE "Code" = 'SUPER_ADMIN'
    LIMIT 1;

    IF v_super_admin_role_id IS NULL THEN
        RAISE EXCEPTION 'SUPER_ADMIN role must exist before seeding the test user.';
    END IF;

    SELECT "Id" INTO v_test_user_id
    FROM public."Users"
    WHERE lower("Email") = 'test@126.com'
    LIMIT 1;

    IF v_test_user_id IS NULL THEN
        INSERT INTO public."Users"
        (
            "Id",
            "UserName",
            "PasswordHash",
            "Email",
            "PhoneNumber",
            "Status",
            "LastLoginTime",
            "CreatedTime",
            "UpdatedTime",
            "IsDeleted"
        )
        VALUES
        (
            uuid_generate_v4(),
            'test',
            '$2a$11$pv/sfVclB/5.5PZjv7JuCevw1EwNTy6V/G9KnYcITxsUpzuVMJ5yW',
            'test@126.com',
            NULL,
            1,
            NULL,
            timezone('utc', now()),
            NULL,
            false
        )
        RETURNING "Id" INTO v_test_user_id;

        RAISE NOTICE 'Test user test@126.com created: %', v_test_user_id;
    ELSE
        UPDATE public."Users"
        SET
            "UserName" = 'test',
            "Status" = 1,
            "IsDeleted" = false,
            "UpdatedTime" = timezone('utc', now())
        WHERE "Id" = v_test_user_id;

        RAISE NOTICE 'Test user test@126.com already exists: %', v_test_user_id;
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM public."UserRoles"
        WHERE "UserId" = v_test_user_id
          AND "RoleId" = v_super_admin_role_id
    ) THEN
        INSERT INTO public."UserRoles" ("Id", "UserId", "RoleId", "CreatedTime")
        VALUES (
            uuid_generate_v4(),
            v_test_user_id,
            v_super_admin_role_id,
            timezone('utc', now())
        );

        RAISE NOTICE 'SUPER_ADMIN granted to test user: %', v_test_user_id;
    ELSE
        RAISE NOTICE 'Test user already has SUPER_ADMIN role: %', v_test_user_id;
    END IF;
END $$;

-- =============================================
-- 2. 创建测试产品
-- =============================================
DO $$
DECLARE
    v_product1_id UUID;
    v_product2_id UUID;
BEGIN
    -- 产品1: CryptoCycleAI
    SELECT "Id" INTO v_product1_id FROM public."Products" WHERE "Code" = 'CRYPTO_CYCLE_AI' LIMIT 1;
    
    IF v_product1_id IS NULL THEN
        INSERT INTO public."Products" ("Id", "Code", "Name", "Description", "IsActive", "Metadata", "CreatedTime")
        VALUES (
            uuid_generate_v4(),
            'CRYPTO_CYCLE_AI',
            'CryptoCycleAI',
            '加密货币周期分析AI工具 - 利用人工智能技术分析加密货币市场周期，提供买卖时机建议',
            true,
            '{"features":["实时市场分析","AI预测模型","多币种支持","历史数据回测"],"category":"AI工具"}'::jsonb,
            timezone('utc', now())
        )
        RETURNING "Id" INTO v_product1_id;
        
        RAISE NOTICE 'Product CRYPTO_CYCLE_AI created: %', v_product1_id;
        
        -- 为产品1添加价格
        INSERT INTO public."Prices" ("Id", "ProductId", "Name", "BillingPeriod", "Amount", "Currency", "IsActive", "CreatedTime")
        VALUES
            (uuid_generate_v4(), v_product1_id, 'Weekly Plan', 'week', 2900, 'USD', true, timezone('utc', now())),
            (uuid_generate_v4(), v_product1_id, 'Monthly Plan', 'month', 9900, 'USD', true, timezone('utc', now())),
            (uuid_generate_v4(), v_product1_id, 'Yearly Plan', 'year', 99900, 'USD', true, timezone('utc', now()));
        
        RAISE NOTICE 'Prices created for CRYPTO_CYCLE_AI';
    ELSE
        RAISE NOTICE 'Product CRYPTO_CYCLE_AI already exists: %', v_product1_id;
    END IF;
    
    -- 产品2: ClinicVoiceAI
    SELECT "Id" INTO v_product2_id FROM public."Products" WHERE "Code" = 'CLINIC_VOICE_AI' LIMIT 1;
    
    IF v_product2_id IS NULL THEN
        INSERT INTO public."Products" ("Id", "Code", "Name", "Description", "IsActive", "Metadata", "CreatedTime")
        VALUES (
            uuid_generate_v4(),
            'CLINIC_VOICE_AI',
            'ClinicVoiceAI',
            '医疗语音转病历工具 - 自动将医生口述转换为标准化电子病历，节省时间提高效率',
            true,
            '{"features":["语音识别","病历模板","医学术语库","多语言支持"],"category":"医疗SaaS"}'::jsonb,
            timezone('utc', now())
        )
        RETURNING "Id" INTO v_product2_id;
        
        RAISE NOTICE 'Product CLINIC_VOICE_AI created: %', v_product2_id;
        
        -- 为产品2添加价格
        INSERT INTO public."Prices" ("Id", "ProductId", "Name", "BillingPeriod", "Amount", "Currency", "IsActive", "CreatedTime")
        VALUES
            (uuid_generate_v4(), v_product2_id, 'Monthly Plan', 'month', 19900, 'USD', true, timezone('utc', now())),
            (uuid_generate_v4(), v_product2_id, 'Yearly Plan', 'year', 199900, 'USD', true, timezone('utc', now()));
        
        RAISE NOTICE 'Prices created for CLINIC_VOICE_AI';
    ELSE
        RAISE NOTICE 'Product CLINIC_VOICE_AI already exists: %', v_product2_id;
    END IF;
END $$;

-- =============================================
-- 3. 查询验证
-- =============================================

-- 查看所有角色
SELECT "Id", "Name", "Code", "Description", "CreatedTime"
FROM public."Roles"
ORDER BY "Code";

-- 查看测试用户及角色
SELECT
    u."Id",
    u."Email",
    u."UserName",
    u."Status",
    r."Code" AS "RoleCode",
    u."CreatedTime",
    u."UpdatedTime"
FROM public."Users" u
LEFT JOIN public."UserRoles" ur ON ur."UserId" = u."Id"
LEFT JOIN public."Roles" r ON r."Id" = ur."RoleId"
WHERE lower(u."Email") = 'test@126.com';

-- 查看所有产品
SELECT "Id", "Code", "Name", "Description", "IsActive", "CreatedTime"
FROM public."Products"
ORDER BY "Code";

-- 查看所有价格
SELECT 
    p."Id",
    p."ProductId",
    prod."Code" AS "ProductCode",
    prod."Name" AS "ProductName",
    p."Name" AS "PriceName",
    p."BillingPeriod",
    p."Amount" / 100.0 AS "AmountUSD",
    p."Currency",
    p."IsActive"
FROM public."Prices" p
INNER JOIN public."Products" prod ON p."ProductId" = prod."Id"
ORDER BY prod."Code", p."Amount";

-- 统计信息
SELECT 
    'Roles' AS "TableName", COUNT(*) AS "Count" FROM public."Roles"
UNION ALL
SELECT 
    'Products' AS "TableName", COUNT(*) AS "Count" FROM public."Products"
UNION ALL
SELECT 
    'Prices' AS "TableName", COUNT(*) AS "Count" FROM public."Prices"
UNION ALL
SELECT 
    'Users' AS "TableName", COUNT(*) AS "Count" FROM public."Users";
