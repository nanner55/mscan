!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
!!! DMA Read(5)/Write(4) arbitrary addresses
!!! 88590015
!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

.ORG 0xF30
dma_enable: .byte 0x3

.ORG 0x9130
! handle mode 87, compress out checks
! support packet counts 0x01->0x100 * 0x2C
mov     r0, r9
mov.w   @(0x10, r14), r0
tst     #0x40, r0
bt      get_ram_buffer_info:
mov     r9, r0
tst     #0x1, r0
bt      get_ram_buffer_info:
bra     check_offset:
nop
check_new:
bt      skip:
mov.l   RAM_DMA_RX_BUF, r10
mov.b   @(3, r10), r0
tst     #0x4, r0
bt      skip:
mov.b   @(2, r10), r0
dt      r0
mov.b   r0, @(2, r10)
bt      skip:
mov.w   @(0x10, r14), r0
mov     #8, r11
shll8   r11
xor     r11, r0
mov.w   r0, @(0x10, r14)
mov.l   @(4, r10), r0
add     #0x2c, r0
mov.l   r0, @(4, r10)
skip:
bra     exit:
nop
.ORG 0x9168
check_offset:
mov.w   @(4, r8), r0
extu.w  r0, r10
mov     r10, r8

! Reads
.ORG 0x919A
mov.l   RAM_DMA_RX_BUF, r10
mov.w   @(2,r10), r0
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
mov.b   @(3,r10), r0
tst     #4, r0
bt      memcpy_read_tgt:
mov.w   @(6,r10), r0
extu.w  r0, r4
swap.w  r4, r4
or      r8, r4
swap.w  r4, r4
bra     memcpy_read_tgt:
mov     #0x2c, r6

.ORG 0x91C8
get_ram_buffer_info: nop

.ORG 0x924C
memcpy_read_tgt: nop

.ORG 0x929A
bra     check_new:
tst     #0x40, r0

.ORG 0x93B4
RAM_DMA_MODE87_BUFFER:            .long 0x12345678
.ORG 0x93CC
RAM_DMA_RX_BUF:                   .long 0x12345678

!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

.ORG 0x9464
extu.b  r0, r0                      ! need to skip byte count
tst     #3, r0
bf      nowrite:

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
mov.w   @(2,r2), r0
tst     #4, r0
bt      memcpy_write_tgt:
mov     #0xff, r5
shll16  r5
or      r9, r5
mov     r0, r6
shlr8   r6            ! grab byte count
memcpy_write_tgt:
mov.l   memcpy, r10
jsr     @r10
add     #6, r4
nowrite:
nop

.ORG 0x9594
exit: nop

.ORG 0x9678
sub:                              .long 0x12345678
.ORG 0x9680
memcpy:                           .long 0x12345678
.ORG 0x9684
RAM_DMA_MODE87_BUFFER2:           .long 0x12345678
