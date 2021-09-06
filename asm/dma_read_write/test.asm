!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
!!! DMA Read(5)/Write(4) arbitrary addresses
!!! 88590015
!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

.ORG 0xF30
dma_enable: .byte 0x3

.ORG 0x919A

! Reads
mov     #1, r0
mov.w   r0, @(2,r2)
extu.w  r8, r0
mov.b   r0, @(5,r2)
shlr8   r0
mov.b   r0, @(4,r2)
mov.w   @(0x14,r14), r0
extu.w  r0, r6
mov     r2, r5
add     #6, r5
mov.l   RAM_DMA_MODE87_BUFFER, r4
add     r8, r4
mov.l   RAM_DMA_RX_BUF, r10
mov.b   @(3,r10), r0
tst     #4, r0
bt      tgt:
mov.w   @(6,r10), r0
extu.w  r0, r4
swap.w  r4, r4
or      r8, r4
swap.w  r4, r4
bra     tgt:
mov     #0x2c, r6

.ORG 0x924C
tgt:
nop ! do not change this code

.ORG 0x93B4
RAM_DMA_MODE87_BUFFER:            .long 0x12345678
.ORG 0x93CC
RAM_DMA_RX_BUF:                   .long 0x12345678

!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

.ORG 0x9486
mov     #0x2c, r6

.ORG 0x949A
cmp/hi  r10, r0
bf      fix:
mov     r10, r4
mov.l   sub, r10
jsr     @r10
mov     r8, r5
extu.w  r0, r6

.ORG 0x94A8
! Writes
fix:
mov.w   @(0x14,r14), r0
extu.w  r0, r9
mov.l   RAM_DMA_MODE87_BUFFER2, r5
add     r9, r5
mov     r2, r4

mov.b   @(3,r2), r0
tst     #4, r0
bt      tgt2:
xor     r5, r5
not     r5, r5
shll16  r5
or      r9, r5
mov     #0x2c, r6
tgt2:
mov.l   memcpy, r10
jsr     @r10
add     #6, r4

.ORG 0x9678
sub:                              .long 0x12345678
.ORG 0x9680
memcpy:                           .long 0x12345678
.ORG 0x9684
RAM_DMA_MODE87_BUFFER2:           .long 0x12345678
