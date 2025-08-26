-- Realtime Chat Platform Database Schema
-- PostgreSQL 15+ with JSONB support

-- Enable required extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_trgm"; -- For text search
CREATE EXTENSION IF NOT EXISTS "btree_gin"; -- For GIN indexes on arrays

-- Create custom types
CREATE TYPE user_status AS ENUM ('active', 'inactive', 'suspended', 'deleted');
CREATE TYPE room_type AS ENUM ('direct', 'group', 'channel');
CREATE TYPE user_role AS ENUM ('member', 'moderator', 'admin', 'owner');
CREATE TYPE message_type AS ENUM ('text', 'image', 'file', 'system', 'thread_reply');
CREATE TYPE receipt_type AS ENUM ('delivered', 'read');
CREATE TYPE outbox_status AS ENUM ('pending', 'processing', 'completed', 'failed');

-- Tenants table
CREATE TABLE tenants (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(100) NOT NULL,
    domain VARCHAR(255) UNIQUE NOT NULL,
    status user_status NOT NULL DEFAULT 'active',
    max_users INTEGER NOT NULL DEFAULT 10000,
    max_rooms INTEGER NOT NULL DEFAULT 1000,
    max_storage_gb INTEGER NOT NULL DEFAULT 100,
    settings JSONB NOT NULL DEFAULT '{}',
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    CONSTRAINT tenants_name_length CHECK (char_length(name) >= 2),
    CONSTRAINT tenants_domain_format CHECK (domain ~ '^[a-zA-Z0-9][a-zA-Z0-9-]{1,61}[a-zA-Z0-9]\.[a-zA-Z]{2,}$'),
    CONSTRAINT tenants_max_users_positive CHECK (max_users > 0),
    CONSTRAINT tenants_max_rooms_positive CHECK (max_rooms > 0),
    CONSTRAINT tenants_max_storage_positive CHECK (max_storage_gb > 0)
);

-- Users table
CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    email VARCHAR(255) NOT NULL,
    username VARCHAR(100) NOT NULL,
    display_name VARCHAR(255),
    avatar_url TEXT,
    status user_status NOT NULL DEFAULT 'active',
    last_seen TIMESTAMP WITH TIME ZONE,
    metadata JSONB NOT NULL DEFAULT '{}',
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    CONSTRAINT users_email_length CHECK (char_length(email) >= 5),
    CONSTRAINT users_username_length CHECK (char_length(username) >= 3),
    CONSTRAINT users_display_name_length CHECK (char_length(display_name) >= 1),
    CONSTRAINT users_email_format CHECK (email ~ '^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$'),
    CONSTRAINT users_username_format CHECK (username ~ '^[a-zA-Z0-9_-]+$')
);

-- Rooms table
CREATE TABLE rooms (
    id VARCHAR(100) NOT NULL,
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    name VARCHAR(255) NOT NULL,
    description TEXT,
    type room_type NOT NULL,
    created_by UUID NOT NULL REFERENCES users(id) ON DELETE SET NULL,
    is_private BOOLEAN NOT NULL DEFAULT false,
    is_archived BOOLEAN NOT NULL DEFAULT false,
    max_members INTEGER NOT NULL DEFAULT 1000,
    settings JSONB NOT NULL DEFAULT '{}',
    metadata JSONB NOT NULL DEFAULT '{}',
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    PRIMARY KEY (tenant_id, id),
    CONSTRAINT rooms_name_length CHECK (char_length(name) >= 1),
    CONSTRAINT rooms_max_members_positive CHECK (max_members > 0),
    CONSTRAINT rooms_id_format CHECK (id ~ '^[a-zA-Z0-9_-]+$')
);

-- Memberships table
CREATE TABLE memberships (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    room_id VARCHAR(100) NOT NULL,
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    role user_role NOT NULL DEFAULT 'member',
    joined_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    left_at TIMESTAMP WITH TIME ZONE,
    is_active BOOLEAN NOT NULL DEFAULT true,
    metadata JSONB NOT NULL DEFAULT '{}',
    
    FOREIGN KEY (tenant_id, room_id) REFERENCES rooms(tenant_id, id) ON DELETE CASCADE,
    UNIQUE (tenant_id, room_id, user_id),
    CONSTRAINT memberships_active_check CHECK (
        (is_active = true AND left_at IS NULL) OR 
        (is_active = false AND left_at IS NOT NULL)
    )
);

-- Messages table
CREATE TABLE messages (
    id VARCHAR(26) PRIMARY KEY, -- ULID format
    tenant_id UUID NOT NULL,
    room_id VARCHAR(100) NOT NULL,
    sender_id UUID NOT NULL REFERENCES users(id) ON DELETE SET NULL,
    message_type message_type NOT NULL DEFAULT 'text',
    content TEXT NOT NULL,
    thread_id VARCHAR(26) REFERENCES messages(id) ON DELETE CASCADE,
    reply_to_id VARCHAR(26) REFERENCES messages(id) ON DELETE SET NULL,
    metadata JSONB NOT NULL DEFAULT '{}',
    created_at BIGINT NOT NULL, -- Unix timestamp in milliseconds
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    FOREIGN KEY (tenant_id, room_id) REFERENCES rooms(tenant_id, id) ON DELETE CASCADE,
    CONSTRAINT messages_content_length CHECK (char_length(content) <= 10000),
    CONSTRAINT messages_created_at_positive CHECK (created_at > 0),
    CONSTRAINT messages_id_format CHECK (id ~ '^[0-9A-Z]{26}$')
);

-- Attachments table
CREATE TABLE attachments (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    message_id VARCHAR(26) NOT NULL REFERENCES messages(id) ON DELETE CASCADE,
    tenant_id UUID NOT NULL,
    name VARCHAR(255) NOT NULL,
    url TEXT NOT NULL,
    size BIGINT NOT NULL,
    mime_type VARCHAR(127) NOT NULL,
    metadata JSONB NOT NULL DEFAULT '{}',
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    CONSTRAINT attachments_name_length CHECK (char_length(name) >= 1),
    CONSTRAINT attachments_size_positive CHECK (size > 0),
    CONSTRAINT attachments_url_format CHECK (url ~ '^https?://'),
    CONSTRAINT attachments_mime_type_format CHECK (mime_type ~ '^[a-zA-Z0-9.-]+/[a-zA-Z0-9.-]+$')
);

-- Read receipts table
CREATE TABLE receipts (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    room_id VARCHAR(100) NOT NULL,
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    message_id VARCHAR(26) NOT NULL REFERENCES messages(id) ON DELETE CASCADE,
    receipt_type receipt_type NOT NULL,
    timestamp BIGINT NOT NULL, -- Unix timestamp in milliseconds
    metadata JSONB NOT NULL DEFAULT '{}',
    
    FOREIGN KEY (tenant_id, room_id) REFERENCES rooms(tenant_id, id) ON DELETE CASCADE,
    UNIQUE (tenant_id, room_id, user_id, message_id, receipt_type),
    CONSTRAINT receipts_timestamp_positive CHECK (timestamp > 0)
);

-- Outbox table for reliable message delivery
CREATE TABLE outbox (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    topic VARCHAR(100) NOT NULL,
    payload JSONB NOT NULL,
    status outbox_status NOT NULL DEFAULT 'pending',
    retry_count INTEGER NOT NULL DEFAULT 0,
    max_retries INTEGER NOT NULL DEFAULT 3,
    next_retry_at TIMESTAMP WITH TIME ZONE,
    error_message TEXT,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    dispatched_at TIMESTAMP WITH TIME ZONE,
    
    CONSTRAINT outbox_topic_length CHECK (char_length(topic) >= 1),
    CONSTRAINT outbox_retry_count_non_negative CHECK (retry_count >= 0),
    CONSTRAINT outbox_max_retries_positive CHECK (max_retries > 0),
    CONSTRAINT outbox_status_retry_check CHECK (
        (status = 'pending' AND next_retry_at IS NULL) OR
        (status IN ('processing', 'failed') AND next_retry_at IS NOT NULL)
    )
);

-- Rate limiting table
CREATE TABLE rate_limits (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    user_id UUID REFERENCES users(id) ON DELETE CASCADE,
    connection_id VARCHAR(255),
    resource_type VARCHAR(50) NOT NULL, -- 'message', 'typing', 'presence'
    bucket_key VARCHAR(255) NOT NULL,
    tokens_remaining INTEGER NOT NULL,
    last_refill_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    CONSTRAINT rate_limits_resource_type_check CHECK (resource_type IN ('message', 'typing', 'presence')),
    CONSTRAINT rate_limits_tokens_remaining_non_negative CHECK (tokens_remaining >= 0),
    CONSTRAINT rate_limits_bucket_key_unique UNIQUE (tenant_id, user_id, connection_id, resource_type, bucket_key)
);

-- Idempotency table for duplicate prevention
CREATE TABLE idempotency_keys (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    key_hash VARCHAR(64) NOT NULL, -- SHA-256 hash of idempotency key
    resource_type VARCHAR(50) NOT NULL,
    resource_id VARCHAR(255),
    expires_at TIMESTAMP WITH TIME ZONE NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    CONSTRAINT idempotency_keys_key_hash_length CHECK (char_length(key_hash) = 64),
    CONSTRAINT idempotency_keys_resource_type_check CHECK (resource_type IN ('message', 'room_join', 'room_leave')),
    UNIQUE (tenant_id, key_hash)
);

-- Audit log table
CREATE TABLE audit_logs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL,
    user_id UUID REFERENCES users(id) ON DELETE SET NULL,
    action VARCHAR(100) NOT NULL,
    resource_type VARCHAR(50) NOT NULL,
    resource_id VARCHAR(255),
    details JSONB NOT NULL DEFAULT '{}',
    ip_address INET,
    user_agent TEXT,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    CONSTRAINT audit_logs_action_length CHECK (char_length(action) >= 1),
    CONSTRAINT audit_logs_resource_type_length CHECK (char_length(resource_type) >= 1)
);

-- Create indexes for performance
CREATE INDEX idx_tenants_domain ON tenants(domain);
CREATE INDEX idx_tenants_status ON tenants(status);
CREATE INDEX idx_tenants_created_at ON tenants(created_at);

CREATE INDEX idx_users_tenant_email ON users(tenant_id, email);
CREATE INDEX idx_users_tenant_username ON users(tenant_id, username);
CREATE INDEX idx_users_status ON users(status);
CREATE INDEX idx_users_last_seen ON users(last_seen);
CREATE INDEX idx_users_metadata ON users USING GIN (metadata);

CREATE INDEX idx_rooms_tenant_type ON rooms(tenant_id, type);
CREATE INDEX idx_rooms_created_by ON rooms(created_by);
CREATE INDEX idx_rooms_created_at ON rooms(created_at);
CREATE INDEX idx_rooms_metadata ON rooms USING GIN (metadata);
CREATE INDEX idx_rooms_name_trgm ON rooms USING GIN (name gin_trgm_ops);

CREATE INDEX idx_memberships_tenant_room ON memberships(tenant_id, room_id);
CREATE INDEX idx_memberships_user_rooms ON memberships(user_id);
CREATE INDEX idx_memberships_role ON memberships(role);
CREATE INDEX idx_memberships_joined_at ON memberships(joined_at);
CREATE INDEX idx_memberships_active ON memberships(is_active);

CREATE INDEX idx_messages_tenant_room_time ON messages(tenant_id, room_id, created_at DESC);
CREATE INDEX idx_messages_sender_time ON messages(sender_id, created_at DESC);
CREATE INDEX idx_messages_thread_id ON messages(thread_id);
CREATE INDEX idx_messages_reply_to ON messages(reply_to_id);
CREATE INDEX idx_messages_metadata ON messages USING GIN (metadata);
CREATE INDEX idx_messages_content_trgm ON messages USING GIN (content gin_trgm_ops);

CREATE INDEX idx_attachments_message_id ON attachments(message_id);
CREATE INDEX idx_attachments_tenant_id ON attachments(tenant_id);
CREATE INDEX idx_attachments_mime_type ON attachments(mime_type);

CREATE INDEX idx_receipts_tenant_room_user ON receipts(tenant_id, room_id, user_id);
CREATE INDEX idx_receipts_message_id ON receipts(message_id);
CREATE INDEX idx_receipts_timestamp ON receipts(timestamp);

CREATE INDEX idx_outbox_tenant_topic_status ON outbox(tenant_id, topic, status);
CREATE INDEX idx_outbox_created_at ON outbox(created_at);
CREATE INDEX idx_outbox_next_retry ON outbox(next_retry_at);
CREATE INDEX idx_outbox_payload ON outbox USING GIN (payload);

CREATE INDEX idx_rate_limits_tenant_user ON rate_limits(tenant_id, user_id);
CREATE INDEX idx_rate_limits_bucket_key ON rate_limits(bucket_key);
CREATE INDEX idx_rate_limits_last_refill ON rate_limits(last_refill_at);

CREATE INDEX idx_idempotency_keys_tenant_hash ON idempotency_keys(tenant_id, key_hash);
CREATE INDEX idx_idempotency_keys_expires ON idempotency_keys(expires_at);

CREATE INDEX idx_audit_logs_tenant_user ON audit_logs(tenant_id, user_id);
CREATE INDEX idx_audit_logs_action ON audit_logs(action);
CREATE INDEX idx_audit_logs_resource ON audit_logs(resource_type, resource_id);
CREATE INDEX idx_audit_logs_created_at ON audit_logs(created_at);
CREATE INDEX idx_audit_logs_details ON audit_logs USING GIN (details);

-- Create partial indexes for common queries
CREATE INDEX idx_messages_active_threads ON messages(tenant_id, room_id, created_at DESC) 
WHERE thread_id IS NOT NULL;

CREATE INDEX idx_memberships_active_members ON memberships(tenant_id, room_id, user_id) 
WHERE is_active = true;

CREATE INDEX idx_outbox_pending_dispatch ON outbox(tenant_id, status, created_at) 
WHERE status = 'pending';

-- Create functions for automatic timestamp updates
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ language 'plpgsql';

-- Create triggers for automatic timestamp updates
CREATE TRIGGER update_tenants_updated_at BEFORE UPDATE ON tenants
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_users_updated_at BEFORE UPDATE ON users
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_rooms_updated_at BEFORE UPDATE ON rooms
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

-- Create function for cleaning up expired data
CREATE OR REPLACE FUNCTION cleanup_expired_data()
RETURNS void AS $$
BEGIN
    -- Clean up expired idempotency keys
    DELETE FROM idempotency_keys WHERE expires_at < NOW();
    
    -- Clean up old audit logs (keep 1 year)
    DELETE FROM audit_logs WHERE created_at < NOW() - INTERVAL '1 year';
    
    -- Clean up failed outbox messages older than 7 days
    DELETE FROM outbox WHERE status = 'failed' AND created_at < NOW() - INTERVAL '7 days';
END;
$$ LANGUAGE plpgsql;

-- Create function for message search
CREATE OR REPLACE FUNCTION search_messages(
    p_tenant_id UUID,
    p_room_id VARCHAR(100),
    p_query TEXT,
    p_limit INTEGER DEFAULT 50,
    p_offset INTEGER DEFAULT 0
)
RETURNS TABLE(
    id VARCHAR(26),
    content TEXT,
    sender_id UUID,
    created_at BIGINT,
    rank REAL
) AS $$
BEGIN
    RETURN QUERY
    SELECT 
        m.id,
        m.content,
        m.sender_id,
        m.created_at,
        ts_rank(to_tsvector('english', m.content), plainto_tsquery('english', p_query)) as rank
    FROM messages m
    WHERE m.tenant_id = p_tenant_id 
        AND m.room_id = p_room_id
        AND to_tsvector('english', m.content) @@ plainto_tsquery('english', p_query)
    ORDER BY rank DESC, m.created_at DESC
    LIMIT p_limit OFFSET p_offset;
END;
$$ LANGUAGE plpgsql;

-- Create views for common queries
CREATE VIEW active_room_members AS
SELECT 
    m.tenant_id,
    m.room_id,
    m.user_id,
    u.username,
    u.display_name,
    u.avatar_url,
    m.role,
    m.joined_at,
    u.last_seen,
    u.status as user_status
FROM memberships m
JOIN users u ON m.user_id = u.id
WHERE m.is_active = true AND u.status = 'active';

CREATE VIEW room_message_counts AS
SELECT 
    tenant_id,
    room_id,
    COUNT(*) as message_count,
    MAX(created_at) as last_message_at,
    COUNT(DISTINCT sender_id) as unique_senders
FROM messages
GROUP BY tenant_id, room_id;

-- Grant permissions (adjust as needed for your environment)
-- GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO chat_app;
-- GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO chat_app;

-- Insert sample data for testing
INSERT INTO tenants (id, name, domain, max_users, max_rooms, max_storage_gb) VALUES
('550e8400-e29b-41d4-a716-446655440001', 'Acme Corporation', 'acme-corp.com', 10000, 1000, 100),
('550e8400-e29b-41d4-a716-446655440002', 'Tech Startup', 'techstartup.io', 5000, 500, 50);

INSERT INTO users (id, tenant_id, email, username, display_name, status) VALUES
('550e8400-e29b-41d4-a716-446655440003', '550e8400-e29b-41d4-a716-446655440001', 'john@acme-corp.com', 'john_doe', 'John Doe', 'active'),
('550e8400-e29b-41d4-a716-446655440004', '550e8400-e29b-41d4-a716-446655440001', 'jane@acme-corp.com', 'jane_smith', 'Jane Smith', 'active');

INSERT INTO rooms (id, tenant_id, name, description, type, created_by, is_private) VALUES
('general', '550e8400-e29b-41d4-a716-446655440001', 'General Discussion', 'Company-wide general discussion', 'channel', '550e8400-e29b-41d4-a716-446655440003', false),
('random', '550e8400-e29b-41d4-a716-446655440001', 'Random', 'Random topics and fun', 'channel', '550e8400-e29b-41d4-a716-446655440004', false);

INSERT INTO memberships (tenant_id, room_id, user_id, role) VALUES
('550e8400-e29b-41d4-a716-446655440001', 'general', '550e8400-e29b-41d4-a716-446655440003', 'member'),
('550e8400-e29b-41d4-a716-446655440001', 'general', '550e8400-e29b-41d4-a716-446655440004', 'member'),
('550e8400-e29b-41d4-a716-446655440001', 'random', '550e8400-e29b-41d4-a716-446655440003', 'member'),
('550e8400-e29b-41d4-a716-446655440001', 'random', '550e8400-e29b-41d4-a716-446655440004', 'member');
