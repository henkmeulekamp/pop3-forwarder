# POP3 Email Forwarder

Small console app that gets email from pop3 and forwards to given smtp service.
Build this to keep forwarding an email to my gmail. Gmail dropped support for reading an pop3 account into an existing gmail inbox in January 2026.
  
Pushed to dockerhub for easy download into Synology https://hub.docker.com/repositories/henkmeulekamp  
  
## 1. Environment Variables

The application can be configured using the following environment variables for Docker:

### POP3 Settings
- `Pop3Settings__Host` - POP3 server hostname
- `Pop3Settings__Port` - POP3 server port (typically 995 for SSL)
- `Pop3Settings__UseSsl` - Enable SSL/TLS (true/false)
- `Pop3Settings__CheckCertificateRevocation` - Check certificate revocation (true/false)
- `Pop3Settings__Username` - POP3 account username
- `Pop3Settings__Password` - POP3 account password

### SMTP Settings
- `SmtpSettings__Host` - SMTP server hostname
- `SmtpSettings__Port` - SMTP server port (typically 465 for SSL, 587 for TLS)
- `SmtpSettings__UseSsl` - Enable SSL/TLS (true/false)
- `SmtpSettings__CheckCertificateRevocation` - Check certificate revocation (true/false)
- `SmtpSettings__Username` - SMTP account username
- `SmtpSettings__Password` - SMTP account password
- `SmtpSettings__ForwardTo` - Email address to forward messages to

## 2. What the Application Does

This application continuously monitors a POP3 email account and forwards all incoming emails to a specified address via SMTP. It:

1. Connects to a POP3 server every 60 seconds
2. Retrieves all messages from the inbox
3. Forwards each message to the configured recipient via SMTP
4. Deletes successfully forwarded messages from the POP3 server
5. Repeats the process indefinitely

The forwarded emails preserve the original subject and body, but use the configured SMTP account as the sender.

## 3. Running with Docker CLI

```shell
# build
docker build -t pop3-forwarder .

# run given settings
docker run \
  -e Pop3Settings__Host=pop.example.com \
  -e Pop3Settings__Port=995 \
  -e Pop3Settings__UseSsl=true \
  -e Pop3Settings__CheckCertificateRevocation=false \
  -e Pop3Settings__Username=your-pop3-username \
  -e Pop3Settings__Password=your-pop3-password \
  -e SmtpSettings__Host=smtp.example.com \
  -e SmtpSettings__Port=465 \
  -e SmtpSettings__UseSsl=true \
  -e SmtpSettings__CheckCertificateRevocation=false \
  -e SmtpSettings__Username=your-smtp-username \
  -e SmtpSettings__Password=your-smtp-password \
  -e SmtpSettings__ForwardTo=recipient@example.com \
  pop3-forwarder

# shell 
docker exec -it pop3-forwarder /bin/bash
```

## 4. Running with Docker Compose

First, update the environment variables in [docker-compose.yaml](docker-compose.yaml) with your actual email account settings.
Currently set 
```shell
# Run the application
docker-compose -f ./docker-compose.yml up

# Force recreate and rebuild
docker-compose -f ./docker-compose.yml up --build --force-recreate --no-deps

# Stop the application
docker-compose -f ./docker-compose.yml down
```

# 5. Dockerhub

```shell
docker-compose build
docker push henkmeulekamp/pop3-forwarding:latest

# linux/arm64
docker build -f Dockerfile --platform linux/arm64 -t henkmeulekamp/pop3-forwarding:latest-arm64 .
docker push henkmeulekamp/pop3-forwarding:latest-arm64 

# linux/amd64
docker build -f Dockerfile --platform linux/amd64 -t henkmeulekamp/pop3-forwarding:latest-x86 .
docker push henkmeulekamp/pop3-forwarding:latest-x86 
```