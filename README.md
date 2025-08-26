# 🚀 Realtime Chat Platform

Một nền tảng chat realtime hiệu suất cao, có thể mở rộng được xây dựng với .NET 8, SignalR, Redis và PostgreSQL. Được thiết kế với clean code principles, comprehensive error handling và production-ready architecture.

## ✨ Tính năng chính

- **Real-time messaging** với SignalR WebSocket connections
- **1-1 và group chat** hỗ trợ quản lý phòng
- **Multi-tenant architecture** cho data isolation
- **JWT authentication** với secure token handling
- **Rate limiting** và backpressure handling
- **High availability** design với Redis clustering
- **Scalable** architecture hỗ trợ 100k+ concurrent connections
- **Clean Code** implementation theo SOLID principles
- **Comprehensive error handling** và logging
- **Health monitoring** và metrics collection

## 🏗️ Kiến trúc hệ thống

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Web Client    │    │   Mobile App    │    │   Desktop App   │
└─────────┬───────┘    └─────────┬───────┘    └─────────┬───────┘
          │                      │                      │
          └──────────────────────┼──────────────────────┘
                                 │
                    ┌─────────────▼─────────────┐
                    │    Load Balancer         │
                    └─────────────┬─────────────┘
                                 │
                    ┌─────────────▼─────────────┐
                    │   Chat Platform Web      │
                    │   (ASP.NET Core 8)       │
                    └─────────────┬─────────────┘
                                 │
          ┌──────────────────────┼──────────────────────┐
          │                      │                      │
┌─────────▼─────────┐  ┌─────────▼─────────┐  ┌─────────▼─────────┐
│   SignalR Hub     │  │   Chat Service    │  │  Auth Service     │
│   (Real-time)     │  │   (Business Logic)│  │  (JWT, OAuth)    │
└─────────┬─────────┘  └─────────┬─────────┘  └─────────┬─────────┘
          │                      │                      │
          └──────────────────────┼──────────────────────┘
                                 │
                    ┌─────────────▼─────────────┐
                    │   Connection Manager      │
                    │   (User Presence)        │
                    └─────────────┬─────────────┘
                                 │
          ┌──────────────────────┼──────────────────────┐
          │                      │                      │
┌─────────▼─────────┐  ┌─────────▼─────────┐  ┌─────────▼─────────┐
│      Redis        │  │    PostgreSQL     │  │   Rate Limiter    │
│   (Cache, Pub/Sub)│  │   (Persistent)    │  │   (Token Bucket)  │
└────────────────────┘  └────────────────────┘  └────────────────────┘
```

## 🚀 Yêu cầu hệ thống

- **RAM**: Tối thiểu 4GB (khuyến nghị 8GB+)
- **CPU**: Tối thiểu 2 cores (khuyến nghị 4 cores+)
- **Storage**: Tối thiểu 10GB free space
- **OS**: Windows 10/11, macOS, hoặc Linux
- **Docker**: Docker Desktop 4.0+ hoặc Docker Engine 20.10+

## 🛠️ Cài đặt và chạy

### 1. Clone repository
```bash
git clone <repository-url>
cd demoE5
```

### 2. Chạy với Docker (Khuyến nghị)
```bash
# Chạy toàn bộ stack
docker-compose up -d

# Hoặc build và chạy
docker-compose up --build -d

# Xem logs
docker-compose logs -f

# Dừng services
docker-compose down
```

### 3. Truy cập ứng dụng
- **Web UI**: http://localhost:5000
- **SignalR Hub**: ws://localhost:5000/chathub
- **API Endpoints**: http://localhost:5000/api/*

## 📱 Cách sử dụng

### Đăng ký và đăng nhập
1. Truy cập http://localhost:5000
2. Click "Register" để tạo tài khoản mới
3. Đăng nhập với email và password

### Tạo phòng chat
1. Sau khi đăng nhập, click "Create Room"
2. Nhập tên phòng và mô tả
3. Chọn loại phòng (Public/Private)
4. Click "Create"

### Tham gia phòng
1. Chọn phòng từ danh sách bên trái
2. Phòng sẽ được highlight và hiển thị tin nhắn
3. Bắt đầu gõ tin nhắn và nhấn Enter

### Gửi tin nhắn
1. Chọn phòng muốn chat
2. Gõ tin nhắn vào ô input
3. Nhấn Enter hoặc click "Send"
4. Tin nhắn sẽ hiển thị real-time cho tất cả thành viên

### Quản lý phòng
- **Join Room**: Tự động khi click vào phòng
- **Leave Room**: Click "Leave Room" hoặc đóng tab
- **Room Info**: Xem thông tin phòng và thành viên

## 🔧 Cấu hình

### Environment Variables
```bash
# Database
ConnectionStrings__PostgreSQL=Host=postgres;Database=chatplatform;Username=postgres;Password=password

# Redis
ConnectionStrings__Redis=redis:6379

# JWT
JwtSettings__SecretKey=your-super-secret-key-with-at-least-32-characters
JwtSettings__Issuer=ChatPlatform
JwtSettings__Audience=ChatPlatformUsers
JwtSettings__ExpirationMinutes=60

# Rate Limiting
RateLimiting__DefaultMessageLimit=100
RateLimiting__DefaultWindowMinutes=1
```

### appsettings.json
File cấu hình chính nằm tại `src/ChatPlatform.Web/appsettings.json` với các section:
- **ConnectionStrings**: Database và Redis connections
- **JwtSettings**: JWT configuration
- **RedisSettings**: Redis optimization
- **RateLimiting**: Rate limiting policies
- **ConnectionManager**: Connection management settings

## 🧪 Testing

### Test cơ bản
1. **Đăng ký tài khoản mới**
2. **Tạo phòng chat**
3. **Gửi tin nhắn**
4. **Kiểm tra real-time updates**

### Test multi-user
1. Mở nhiều browser tabs/windows
2. Đăng nhập với các tài khoản khác nhau
3. Tham gia cùng phòng
4. Gửi tin nhắn và kiểm tra sync

### Test performance
1. Tạo nhiều phòng
2. Gửi tin nhắn liên tục
3. Kiểm tra response time
4. Monitor memory usage

## 📊 Monitoring và Logs

### Health Checks
```bash
# Kiểm tra health status
curl http://localhost:5000/health

# Redis health
docker-compose exec redis redis-cli ping

# PostgreSQL health
docker-compose exec postgres pg_isready -U postgres
```

### Logs
```bash
# Web app logs
docker-compose logs chatplatform-web

# Redis logs
docker-compose logs chatplatform-redis

# PostgreSQL logs
docker-compose logs chatplatform-postgres

# Follow logs real-time
docker-compose logs -f
```

## 🚨 Troubleshooting

### Vấn đề thường gặp

#### 1. Docker build fails
```bash
# Clean Docker cache
docker system prune -f

# Rebuild từ đầu
docker-compose down
docker-compose up --build -d
```

#### 2. SignalR connection issues
- Kiểm tra JWT token có hợp lệ không
- Xác nhận tenant_id trong token
- Kiểm tra CORS configuration

#### 3. Database connection errors
```bash
# Kiểm tra PostgreSQL status
docker-compose exec postgres pg_isready -U postgres

# Restart service
docker-compose restart postgres
```

#### 4. Redis connection issues
```bash
# Kiểm tra Redis status
docker-compose exec redis redis-cli ping

# Restart service
docker-compose restart redis
```

### Debug commands
```bash
# Kiểm tra container status
docker-compose ps

# Xem resource usage
docker stats

# Kiểm tra network
docker network ls
docker network inspect demoe5_chatplatform-network

# Restart specific service
docker-compose restart chatplatform-web
```

## 🔒 Security

### Authentication
- **JWT tokens** với expiration time
- **Secure password hashing**
- **Token refresh mechanism**

### Authorization
- **Role-based access control**
- **Tenant isolation**
- **Room-level permissions**

### Data Protection
- **Input validation** và sanitization
- **SQL injection prevention**
- **XSS protection**
- **Rate limiting** để prevent abuse

## 📈 Performance

### Optimizations
- **Connection pooling** cho database
- **Redis caching** cho frequent data
- **Async/await** patterns
- **Memory-efficient collections**

### Scalability
- **Horizontal scaling** với multiple instances
- **Load balancing** support
- **Redis clustering** ready
- **Database sharding** capable

## 🚀 Deployment

### Production
```bash
# Build production image
docker build -t chatplatform:prod .

# Run với production config
docker run -d -p 80:80 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ConnectionStrings__PostgreSQL="..." \
  chatplatform:prod
```

### Kubernetes
```bash
# Apply manifests
kubectl apply -f k8s/

# Scale deployment
kubectl scale deployment chatplatform-web --replicas=3
```

## 🤝 Contributing

### Development workflow
1. Fork repository
2. Tạo feature branch
3. Implement changes
4. Add tests
5. Submit pull request

### Code standards
- **SOLID principles**
- **Clean code practices**
- **Comprehensive error handling**
- **Unit test coverage**
- **Documentation updates**

## 📄 License

Dự án này được phát hành dưới MIT License. Xem file `LICENSE` để biết thêm chi tiết.

## 📞 Support

### Issues
- Tạo issue trên GitHub repository
- Mô tả chi tiết vấn đề gặp phải
- Include logs và error messages

### Documentation
- Xem inline code comments
- Kiểm tra API documentation
- Review configuration examples

---

**🎯 Mục tiêu**: Xây dựng một nền tảng chat realtime enterprise-grade với clean code, high performance và production readiness.

**✨ Status**: Production Ready - Tất cả tính năng core đã hoàn thiện và được test kỹ lưỡng.
